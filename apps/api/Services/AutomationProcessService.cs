using System.Diagnostics;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AutomationProcessService
{
    private const string RequiredExtensionVersion = "0.1.4";
    private readonly JobPilotDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly JobFilterService _filterService;
    private readonly ScoringService _scoring;
    private readonly ApplicationLimitService _limits;
    private readonly AutoApplyPolicyService _policy;

    public AutomationProcessService(
        JobPilotDbContext db,
        JwtTokenService tokens,
        JobFilterService filterService,
        ScoringService scoring,
        ApplicationLimitService limits,
        AutoApplyPolicyService policy)
    {
        _db = db;
        _tokens = tokens;
        _filterService = filterService;
        _scoring = scoring;
        _limits = limits;
        _policy = policy;
    }

    public async Task<IResult> StartDiscoverAsync(Guid userId, AutomationDiscoverRequest request)
    {
        var policy = await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
        var keywords = string.IsNullOrWhiteSpace(request.Keywords) ? ".NET" : request.Keywords;
        var location = string.IsNullOrWhiteSpace(request.Location) ? "Brazil" : request.Location;
        var limit = request.Limit <= 0 ? 25 : request.Limit;
        var executionTarget = ResolveExecutionTarget(policy);

        if (await ValidatePolicyActiveAsync(userId, policy, "buscar vagas") is { } pauseError)
            return pauseError;

        if (ValidateExecutionTarget(userId, executionTarget, "buscar vagas") is { } targetError)
            return targetError;

        if (executionTarget == ExecutionTargets.Extension && await ValidateExtensionReadyAsync(userId, "buscar vagas") is { } extensionError)
            return extensionError;

        var token = await _tokens.CreateAgentTokenAsync(_db, userId, "JobPilot web automation");
        var root = ResolveWorkspaceRoot();
        var browserDriver = ResolveBrowserDriver(executionTarget);
        var headless = browserDriver == "extension" ? "" : " --headless true";
        var browserProfile = browserDriver == "playwright" ? ResolveBrowserProfilePath(userId) : "";
        var profileArg = string.IsNullOrWhiteSpace(browserProfile) ? "" : $" --browser-profile {Quote(browserProfile)}";
        var process = StartAgentProcess(root, $"discover --token {Quote(token)} --api http://localhost:5000 --keywords {Quote(keywords)} --location {Quote(location)} --limit {limit} --browser-driver {Quote(browserDriver)}{headless}{profileArg}");
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.discover.started",
            Detail = $"{keywords} | {location} | limit {limit} | target={executionTarget} | driver={browserDriver} | continuous=false | pid={process.ProcessId} | log={process.LogPath}"
        });
        await _db.SaveChangesAsync();
        return Results.Accepted("/api/events", new { started = true, alreadyRunning = false, mode = "discover", continuous = false, executionTarget, browserDriver, browserProfile, processId = process.ProcessId, logPath = process.LogPath });
    }

    public async Task<IResult> StartApplyAsync(Guid userId, AutomationApplyRequest request)
    {
        var policy = await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
        var maxApplications = request.MaxApplications <= 0 ? policy?.MaxApplicationsPerRun ?? 10 : request.MaxApplications;
        var autoSubmit = request.AutoSubmit;
        var mode = policy?.AutoSubmitMode ?? "conservative";
        var executionTarget = ResolveExecutionTarget(policy);
        var queuedNow = 0;

        if (await ValidatePolicyActiveAsync(userId, policy, "aplicar na fila") is { } pauseError)
            return pauseError;

        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await _filterService.GetOrCreateAsync(userId);
        var dailyLimit = await _limits.GetDailySnapshotAsync(userId, policy);

        if (await GetActiveApplicationRunAsync(userId) is { } activeRun)
        {
            const string message = "Ja existe um piloto automatico em execucao. Aguarde terminar ou revise a execucao em andamento.";
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationJobId = activeRun.ApplicationJobId,
                EventType = "automation.apply.already_running",
                Detail = $"{message} run={activeRun.Id}; startedAt={activeRun.StartedAt:O}"
            });
            await _db.SaveChangesAsync();
            return Results.Ok(new { started = false, alreadyRunning = true, mode = "apply", activeRunId = activeRun.Id, activeRun.StartedAt, autoSubmit, maxApplications, queuedNow, executionTarget, browserDriver = ResolveBrowserDriver(executionTarget), message });
        }

        if (dailyLimit.IsReached)
        {
            var message = $"Limite diario de candidaturas atingido ({dailyLimit.AppliedToday}/{dailyLimit.DailyLimit}).";
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "automation.apply.daily_limit_reached",
                Detail = message
            });
            await _db.SaveChangesAsync();
            return Results.Ok(new { started = false, mode = "apply", autoSubmit, maxApplications = 0, queuedNow, recoveredStaleOpening = 0, executionTarget, browserDriver = ResolveBrowserDriver(executionTarget), dailyLimitReached = true, dailyLimit.DailyLimit, dailyLimit.AppliedToday, dailyLimit.RemainingToday, message });
        }

        maxApplications = Math.Min(maxApplications, dailyLimit.RemainingToday);

        if (ValidateExecutionTarget(userId, executionTarget, "aplicar na fila") is { } targetError)
            return targetError;

        if (executionTarget == ExecutionTargets.Extension && await ValidateExtensionReadyAsync(userId, "aplicar na fila") is { } extensionError)
            return extensionError;

        var browserDriver = ResolveBrowserDriver(executionTarget);
        var browserProfile = browserDriver == "playwright" ? ResolveBrowserProfilePath(userId) : "";
        var recoveredStaleOpening = await RecoverStaleOpeningJobsAsync(userId);
        var alreadyQueued = await _db.ApplicationJobs.AnyAsync(x => x.UserId == userId && x.State == JobStatuses.QueuedToApply);
        if (!alreadyQueued)
            queuedNow = await QueueAutopilotJobsAsync(userId, maxApplications, profile, filter);

        var hasQueuedWork = alreadyQueued || queuedNow > 0 || await _db.ApplicationJobs.AnyAsync(x => x.UserId == userId && x.State == JobStatuses.QueuedToApply);
        if (!hasQueuedWork)
        {
            const string message = "Nenhuma vaga elegivel na fila. Execute a busca ou revise os filtros de remoto/junior antes de iniciar o piloto.";
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "automation.apply.no_queue",
                Detail = recoveredStaleOpening > 0 ? $"{message} recoveredStaleOpening={recoveredStaleOpening}; target={executionTarget}" : $"{message} target={executionTarget}"
            });
            await _db.SaveChangesAsync();
            return Results.Ok(new { started = false, mode = "apply", autoSubmit, maxApplications, queuedNow, recoveredStaleOpening, executionTarget, browserDriver, message });
        }

        var token = await _tokens.CreateAgentTokenAsync(_db, userId, "JobPilot web autopilot");
        var root = ResolveWorkspaceRoot();
        await _db.SaveChangesAsync();
        var profileArg = string.IsNullOrWhiteSpace(browserProfile) ? "" : $" --browser-profile {Quote(browserProfile)}";
        var process = StartAgentProcess(root, $"run --token {Quote(token)} --api http://localhost:5000 --auto-submit {autoSubmit.ToString().ToLowerInvariant()} --max-applications {maxApplications} --policy {Quote(mode)} --browser-driver {Quote(browserDriver)}{profileArg}");
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.apply.started",
            Detail = $"autoSubmit={autoSubmit}; maxApplications={maxApplications}; queuedNow={queuedNow}; recoveredStaleOpening={recoveredStaleOpening}; target={executionTarget}; driver={browserDriver}; profile={browserProfile}; log={process.LogPath}"
        });
        await _db.SaveChangesAsync();
        return Results.Accepted("/api/events", new { started = true, mode = "apply", autoSubmit, maxApplications, queuedNow, recoveredStaleOpening, executionTarget, browserDriver, browserProfile, logPath = process.LogPath });
    }

    private async Task<int> RecoverStaleOpeningJobsAsync(Guid userId)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        var openingJobs = await _db.ApplicationJobs
            .Include(x => x.JobLead)
            .Where(x => x.UserId == userId && x.State == JobStatuses.Opening)
            .ToListAsync();

        var recovered = 0;
        foreach (var job in openingJobs)
        {
            var lastEventAt = await _db.ExecutionEvents
                .Where(x => x.UserId == userId && x.ApplicationJobId == job.Id)
                .MaxAsync(x => (DateTimeOffset?)x.OccurredAt) ?? job.CreatedAt;

            if (lastEventAt >= cutoff)
                continue;

            job.State = JobStatuses.NeedsReview;
            job.Reason = "agent_interrupted";
            if (job.JobLead is not null)
            {
                job.JobLead.Status = JobStatuses.NeedsReview;
                job.JobLead.AutomationReason = "agent_interrupted";
            }

            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationJobId = job.Id,
                EventType = "automation.apply.recovered_stale_opening",
                Detail = $"Recovered stale opening job after no activity since {lastEventAt:O}."
            });
            recovered++;
        }

        if (recovered > 0)
            await _db.SaveChangesAsync();

        return recovered;
    }

    private async Task<AgentRun?> GetActiveApplicationRunAsync(Guid userId)
    {
        var staleCutoff = DateTimeOffset.UtcNow.AddHours(-2);
        var staleRuns = await _db.AgentRuns
            .Where(x => x.UserId == userId && x.Mode == "application" && x.State == "running" && x.StartedAt < staleCutoff)
            .ToListAsync();

        foreach (var staleRun in staleRuns)
        {
            staleRun.State = "failed";
            staleRun.CompletedAt = DateTimeOffset.UtcNow;
        }

        if (staleRuns.Count > 0)
            await _db.SaveChangesAsync();

        return await _db.AgentRuns
            .Where(x => x.UserId == userId && x.Mode == "application" && x.State == "running")
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync();
    }

    private async Task<int> QueueAutopilotJobsAsync(Guid userId, int maxApplications, CandidateProfile? profile, JobFilter filter)
    {
        var retryableStatuses = new[]
        {
            JobStatuses.Found,
            JobStatuses.Scored,
            JobStatuses.Shortlisted,
            JobStatuses.Failed,
            "skipped"
        };

        var candidates = await _db.JobLeads
            .Where(x => x.UserId == userId && retryableStatuses.Contains(x.Status))
            .OrderByDescending(x => x.Score)
            .Take(maxApplications * 4)
            .ToListAsync();

        var queueable = new List<JobLead>();
        foreach (var lead in candidates)
        {
            _scoring.Score(lead, profile, filter);
            if (_filterService.ShouldIgnore(lead, filter, out var reason))
            {
                lead.Status = JobStatuses.Ignored;
                lead.FilteredReason = reason;
                continue;
            }

            var policy = await _policy.EvaluateAsync(userId, lead, profile, filter);
            if (!policy.CanApply)
            {
                lead.Status = JobStatuses.ManualOnly;
                lead.AutomationReason = policy.Reason;
                _db.ExecutionEvents.Add(new ExecutionEvent
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventType = "automation.apply.blocked_by_policy",
                    Detail = $"job={lead.Id}; title={lead.Title}; reason={policy.Reason}"
                });
                continue;
            }

            lead.FilteredReason = "";
            queueable.Add(lead);
            if (queueable.Count >= maxApplications) break;
        }

        if (queueable.Count == 0) return 0;

        var run = new ApplicationRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"Autopilot {DateTimeOffset.Now:yyyy-MM-dd HH:mm}",
            TotalJobs = queueable.Count
        };

        foreach (var lead in queueable)
        {
            lead.Status = JobStatuses.QueuedToApply;
            var existingTask = await _db.ApplicationJobs
                .Where(x => x.UserId == userId && x.JobLeadId == lead.Id && x.State != JobStatuses.Applied)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingTask is not null)
            {
                existingTask.ApplicationRunId = run.Id;
                existingTask.JobUrl = lead.Url;
                existingTask.State = JobStatuses.QueuedToApply;
                existingTask.Reason = "autopilot_retry";
                continue;
            }

            run.Jobs.Add(new ApplicationJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                JobLeadId = lead.Id,
                JobUrl = lead.Url,
                State = JobStatuses.QueuedToApply
            });
        }

        _db.ApplicationRuns.Add(run);
        return queueable.Count;
    }

    private async Task<IResult?> ValidateExtensionReadyAsync(Guid userId, string action)
    {
        var session = await GetConnectedExtensionAsync(userId);
        if (session is null)
            return Results.BadRequest(new { message = $"Conecte a extensao do JobPilot no Chrome antes de {action}." });

        if (!IsVersionAtLeast(session.ExtensionVersion, RequiredExtensionVersion))
        {
            var message = $"A extensao JobPilot conectada esta na versao {DisplayVersion(session.ExtensionVersion)}, mas o fluxo real precisa da versao {RequiredExtensionVersion}. Recarregue a extensao em chrome://extensions e tente novamente.";
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "automation.extension.outdated",
                Detail = message
            });
            await _db.SaveChangesAsync();
            return Results.BadRequest(new { message, requiredVersion = RequiredExtensionVersion, currentVersion = session.ExtensionVersion });
        }

        return null;
    }

    private async Task<IResult?> ValidatePolicyActiveAsync(Guid userId, AgentPolicy? policy, string action)
    {
        if (policy?.IsPaused != true)
            return null;

        var message = $"Automacao pausada pela politica do agente. Retome antes de {action}.";
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.policy.paused",
            Detail = $"{message} action={action}"
        });
        await _db.SaveChangesAsync();
        return Results.Ok(new { started = false, paused = true, mode = action.Contains("buscar", StringComparison.OrdinalIgnoreCase) ? "discover" : "apply", message });
    }

    private async Task<BrowserExtensionSession?> GetConnectedExtensionAsync(Guid userId)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-90);
        return await _db.BrowserExtensionSessions
            .Where(x =>
            x.UserId == userId &&
            x.Status != "revoked" &&
            x.LastHeartbeatAt != null &&
            x.LastHeartbeatAt >= cutoff)
            .OrderByDescending(x => x.LastHeartbeatAt)
            .FirstOrDefaultAsync();
    }

    private static bool IsVersionAtLeast(string value, string minimum)
    {
        var currentParts = ParseVersion(value);
        var minimumParts = ParseVersion(minimum);
        for (var i = 0; i < Math.Max(currentParts.Length, minimumParts.Length); i++)
        {
            var current = i < currentParts.Length ? currentParts[i] : 0;
            var required = i < minimumParts.Length ? minimumParts[i] : 0;
            if (current > required) return true;
            if (current < required) return false;
        }

        return true;
    }

    private static int[] ParseVersion(string value) =>
        (value ?? "")
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();

    private static string DisplayVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? "desconhecida" : version;

    private IResult? ValidateExecutionTarget(Guid userId, string executionTarget, string action)
    {
        if (ExecutionTargets.IsImplemented(executionTarget))
            return null;

        var message = executionTarget switch
        {
            ExecutionTargets.RemoteBrowser => "O worker remoto ainda nao esta habilitado neste MVP local. Use extensao ou navegador local dedicado.",
            ExecutionTargets.ServerConnector => "Conectores server-side ainda estao em beta fechado. Use extensao ou navegador local dedicado.",
            _ => $"Alvo de execucao '{executionTarget}' ainda nao esta habilitado."
        };

        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.execution_target.unsupported",
            Detail = $"{message} action={action}; target={executionTarget}"
        });
        _db.SaveChanges();
        return Results.BadRequest(new { message, executionTarget });
    }

    private static string ResolveExecutionTarget(AgentPolicy? policy)
    {
        var value = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET")
                    ?? Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER")
                    ?? Environment.GetEnvironmentVariable("JOBPILOT_BROWSER_DRIVER")
                    ?? policy?.ExecutionTarget
                    ?? ExecutionTargets.Extension;
        return ExecutionTargets.Normalize(value);
    }

    private static string ResolveBrowserDriver(string executionTarget) =>
        executionTarget == ExecutionTargets.LocalDedicatedBrowser ? "playwright" : "extension";

    private static string ResolveBrowserProfilePath(Guid userId)
    {
        var configured = Environment.GetEnvironmentVariable("JOBPILOT_BROWSER_PROFILE");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? ResolveWorkspaceRoot()
            : localAppData;

        return Path.Combine(root, "JobPilot", "browser-profiles", userId.ToString("N"));
    }

    private static AgentProcessInfo StartAgentProcess(string root, string arguments)
    {
        var logsDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, $"agent-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {Quote(Path.Combine(root, "apps", "agent", "JobPilot.Agent.csproj"))} -- {arguments}",
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => AppendAgentLog(logPath, e.Data);
        process.ErrorDataReceived += (_, e) => AppendAgentLog(logPath, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AppendAgentLog(logPath, $"[start] dotnet {RedactToken(startInfo.Arguments)}");
        return new AgentProcessInfo(logPath, process.Id);
    }

    private static string ResolveWorkspaceRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "apps", "agent")))
                return current.FullName;
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void AppendAgentLog(string logPath, string? line)
    {
        if (line is null) return;
        try
        {
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {line}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent automation from starting.
        }
    }

    private static string RedactToken(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"--token\s+""[^""]+""", "--token \"[redacted]\"");

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}

public sealed record AgentProcessInfo(string LogPath, int ProcessId);
