using System.Diagnostics;
using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AutomationProcessService
{
    private readonly JobPilotDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly JobFilterService _filterService;
    private readonly ScoringService _scoring;
    private readonly ApplicationLimitService _limits;
    private readonly AutoApplyPolicyService _policy;
    private readonly AutomationReadinessService _readiness;

    public AutomationProcessService(
        JobPilotDbContext db,
        JwtTokenService tokens,
        JobFilterService filterService,
        ScoringService scoring,
        ApplicationLimitService limits,
        AutoApplyPolicyService policy,
        AutomationReadinessService readiness)
    {
        _db = db;
        _tokens = tokens;
        _filterService = filterService;
        _scoring = scoring;
        _limits = limits;
        _policy = policy;
        _readiness = readiness;
    }

    public async Task<ServiceResult<AutomationOperationResult>> StartDiscoverAsync(Guid userId, AutomationDiscoverRequest request)
    {
        var policy = await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
        var keywords = string.IsNullOrWhiteSpace(request.Keywords) ? ".NET" : request.Keywords;
        var location = string.IsNullOrWhiteSpace(request.Location) ? "Brazil" : request.Location;
        var limit = request.Limit <= 0 ? 25 : request.Limit;
        var executionTarget = AutomationRuntimeLauncher.ResolveExecutionTarget(policy);

        if (await _readiness.ValidatePolicyActiveAsync(userId, policy, "buscar vagas") is { } pauseError)
            return pauseError;

        if (_readiness.ValidateExecutionTarget(userId, executionTarget, "buscar vagas") is { } targetError)
            return targetError;

        if (executionTarget == ExecutionTargets.Extension && await _readiness.ValidateExtensionReadyAsync(userId, "buscar vagas") is { } extensionError)
            return extensionError;

        if (executionTarget == ExecutionTargets.LocalDedicatedBrowser && await _readiness.ValidatePlatformCredentialReadyAsync(userId, "linkedin", "buscar vagas") is { } credentialError)
            return credentialError;

        var token = await _tokens.CreateAgentTokenAsync(userId, "JobPilot web automation");
        var root = AutomationRuntimeLauncher.ResolveWorkspaceRoot();
        var browserDriver = AutomationRuntimeLauncher.ResolveBrowserDriver(executionTarget);
        var headless = browserDriver == "extension" ? "" : " --headless true";
        var browserProfile = browserDriver == "playwright" ? AutomationRuntimeLauncher.ResolveBrowserProfilePath(userId) : "";
        var profileArg = string.IsNullOrWhiteSpace(browserProfile) ? "" : $" --browser-profile {AutomationRuntimeLauncher.Quote(browserProfile)}";
        var process = AutomationRuntimeLauncher.StartAgentProcess(root, $"discover --token {AutomationRuntimeLauncher.Quote(token)} --api http://localhost:5000 --keywords {AutomationRuntimeLauncher.Quote(keywords)} --location {AutomationRuntimeLauncher.Quote(location)} --limit {limit} --browser-driver {AutomationRuntimeLauncher.Quote(browserDriver)}{headless}{profileArg}");
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.discover.started",
            Detail = $"{keywords} | {location} | limit {limit} | target={executionTarget} | driver={browserDriver} | continuous=false | pid={process.ProcessId} | log={process.LogPath}"
        });
        await _db.SaveChangesAsync();
        return ServiceResult<AutomationOperationResult>.Accepted(
            new AutomationOperationResult(
                true,
                "discover",
                executionTarget,
                browserDriver,
                browserProfile,
                process.LogPath,
                ProcessId: process.ProcessId));
    }

    public async Task<ServiceResult<AutomationOperationResult>> StartApplyAsync(Guid userId, AutomationApplyRequest request)
    {
        var policy = await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
        var maxApplications = request.MaxApplications <= 0 ? policy?.MaxApplicationsPerRun ?? 10 : request.MaxApplications;
        var autoSubmit = request.AutoSubmit;
        var mode = policy?.AutoSubmitMode ?? "conservative";
        var executionTarget = AutomationRuntimeLauncher.ResolveExecutionTarget(policy);
        var queuedNow = 0;

        if (await _readiness.ValidatePolicyActiveAsync(userId, policy, "aplicar na fila") is { } pauseError)
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
            return ServiceResult<AutomationOperationResult>.Ok(
                new AutomationOperationResult(
                    false,
                    "apply",
                    executionTarget,
                    AutomationRuntimeLauncher.ResolveBrowserDriver(executionTarget),
                    Message: message,
                    AlreadyRunning: true,
                    AutoSubmit: autoSubmit,
                    MaxApplications: maxApplications,
                    QueuedNow: queuedNow,
                    ActiveRunId: activeRun.Id,
                    StartedAt: activeRun.StartedAt));
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
            return ServiceResult<AutomationOperationResult>.Ok(
                new AutomationOperationResult(
                    false,
                    "apply",
                    executionTarget,
                    AutomationRuntimeLauncher.ResolveBrowserDriver(executionTarget),
                    Message: message,
                    DailyLimitReached: true,
                    AutoSubmit: autoSubmit,
                    QueuedNow: queuedNow,
                    DailyLimit: dailyLimit.DailyLimit,
                    AppliedToday: dailyLimit.AppliedToday,
                    RemainingToday: dailyLimit.RemainingToday));
        }

        maxApplications = Math.Min(maxApplications, dailyLimit.RemainingToday);

        if (_readiness.ValidateExecutionTarget(userId, executionTarget, "aplicar na fila") is { } targetError)
            return targetError;

        if (executionTarget == ExecutionTargets.Extension && await _readiness.ValidateExtensionReadyAsync(userId, "aplicar na fila") is { } extensionError)
            return extensionError;

        var browserDriver = AutomationRuntimeLauncher.ResolveBrowserDriver(executionTarget);
        var browserProfile = browserDriver == "playwright" ? AutomationRuntimeLauncher.ResolveBrowserProfilePath(userId) : "";
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
            return ServiceResult<AutomationOperationResult>.Ok(
                new AutomationOperationResult(
                    false,
                    "apply",
                    executionTarget,
                    browserDriver,
                    Message: message,
                    AutoSubmit: autoSubmit,
                    MaxApplications: maxApplications,
                    QueuedNow: queuedNow,
                    RecoveredStaleOpening: recoveredStaleOpening));
        }

        var token = await _tokens.CreateAgentTokenAsync(userId, "JobPilot web autopilot");
        var root = AutomationRuntimeLauncher.ResolveWorkspaceRoot();
        await _db.SaveChangesAsync();
        var profileArg = string.IsNullOrWhiteSpace(browserProfile) ? "" : $" --browser-profile {AutomationRuntimeLauncher.Quote(browserProfile)}";
        var process = AutomationRuntimeLauncher.StartAgentProcess(root, $"run --token {AutomationRuntimeLauncher.Quote(token)} --api http://localhost:5000 --auto-submit {autoSubmit.ToString().ToLowerInvariant()} --max-applications {maxApplications} --policy {AutomationRuntimeLauncher.Quote(mode)} --browser-driver {AutomationRuntimeLauncher.Quote(browserDriver)}{profileArg}");
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.apply.started",
            Detail = $"autoSubmit={autoSubmit}; maxApplications={maxApplications}; queuedNow={queuedNow}; recoveredStaleOpening={recoveredStaleOpening}; target={executionTarget}; driver={browserDriver}; profile={browserProfile}; log={process.LogPath}"
        });
        await _db.SaveChangesAsync();
        return ServiceResult<AutomationOperationResult>.Accepted(
            new AutomationOperationResult(
                true,
                "apply",
                executionTarget,
                browserDriver,
                browserProfile,
                process.LogPath,
                AutoSubmit: autoSubmit,
                MaxApplications: maxApplications,
                QueuedNow: queuedNow,
                RecoveredStaleOpening: recoveredStaleOpening));
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

}

public sealed record AgentProcessInfo(string LogPath, int ProcessId);
