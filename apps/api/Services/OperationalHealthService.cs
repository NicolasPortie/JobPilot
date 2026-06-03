using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class OperationalHealthService
{
    private const string RequiredExtensionVersion = "0.1.4";
    private readonly JobPilotDbContext _db;
    private readonly ApplicationLimitService _limits;

    public OperationalHealthService(JobPilotDbContext db, ApplicationLimitService limits)
    {
        _db = db;
        _limits = limits;
    }

    public async Task<OperationalHealthResponse> GetAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        var extensionCutoff = now.AddSeconds(-90);
        var staleCommandCutoff = now.AddSeconds(-45);
        var staleRunCutoff = now.AddHours(-2);
        var staleJobCutoff = now.AddMinutes(-10);

        var databaseOk = await CanConnectAsync();
        var defaultProvider = await _db.AiProviders
            .Where(x => x.UserId == userId && x.IsDefault)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();
        var policy = await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId);
        var executionTarget = ExecutionTargets.Normalize(policy?.ExecutionTarget);
        var dailyLimit = await _limits.GetDailySnapshotAsync(userId, policy);

        var extension = await _db.BrowserExtensionSessions
            .Where(x => x.UserId == userId && x.Status != "revoked")
            .OrderByDescending(x => x.LastHeartbeatAt != null && x.LastHeartbeatAt >= extensionCutoff)
            .ThenByDescending(x => x.LastHeartbeatAt ?? x.CreatedAt)
            .FirstOrDefaultAsync();

        var pendingCommands = 0;
        var staleCommands = 0;
        if (executionTarget == ExecutionTargets.Extension && extension is not null)
        {
            pendingCommands = await _db.BrowserExtensionCommandJobs.CountAsync(x =>
                x.UserId == userId &&
                x.BrowserExtensionSessionId == extension.Id &&
                x.Status == "pending");

            staleCommands = await _db.BrowserExtensionCommandJobs.CountAsync(x =>
                x.UserId == userId &&
                x.BrowserExtensionSessionId == extension.Id &&
                x.Status == "claimed" &&
                x.ClaimedAt != null &&
                x.ClaimedAt < staleCommandCutoff);
        }

        var openCheckpoints = await _db.Checkpoints.CountAsync(x => x.UserId == userId && x.ResolutionStatus == "open");
        var queuedApplications = await _db.ApplicationJobs.CountAsync(x => x.UserId == userId && x.State == JobStatuses.QueuedToApply);
        var runningAgentRuns = await _db.AgentRuns.CountAsync(x => x.UserId == userId && x.State == "running");
        var staleAgentRuns = await _db.AgentRuns.CountAsync(x => x.UserId == userId && x.State == "running" && x.StartedAt < staleRunCutoff);
        var staleApplicationJobs = await CountStaleApplicationJobsAsync(userId, staleJobCutoff);

        var database = new HealthSignal("database", databaseOk ? "ok" : "error", databaseOk ? "Banco conectado" : "Banco indisponivel", "");
        var policySignal = BuildPolicySignal(policy);
        var limit = BuildLimitSignal(dailyLimit);
        var ai = BuildAiSignal(defaultProvider);
        var executor = BuildExecutorSignal(executionTarget, extension, extensionCutoff);
        var commands = BuildCommandSignal(executionTarget, pendingCommands, staleCommands);
        var maintenance = BuildMaintenanceSignal(staleAgentRuns, staleApplicationJobs);
        var queue = BuildQueueSignal(queuedApplications, openCheckpoints, runningAgentRuns);

        var signals = new[] { database, policySignal, limit, ai, executor, commands, maintenance, queue };
        return new OperationalHealthResponse(Rollup(signals), now, signals, new OperationalHealthMetrics(
            queuedApplications,
            openCheckpoints,
            runningAgentRuns,
            pendingCommands,
            staleCommands,
            staleAgentRuns,
            staleApplicationJobs,
            dailyLimit.DailyLimit,
            dailyLimit.AppliedToday,
            dailyLimit.RemainingToday));
    }

    private async Task<int> CountStaleApplicationJobsAsync(Guid userId, DateTimeOffset staleJobCutoff)
    {
        var jobs = await _db.ApplicationJobs
            .Where(x => x.UserId == userId && (x.State == JobStatuses.Opening || x.State == JobStatuses.Filling))
            .Select(x => new { x.Id, x.CreatedAt })
            .ToListAsync();

        var stale = 0;
        foreach (var job in jobs)
        {
            var lastEventAt = await _db.ExecutionEvents
                .Where(x => x.UserId == userId && x.ApplicationJobId == job.Id)
                .MaxAsync(x => (DateTimeOffset?)x.OccurredAt) ?? job.CreatedAt;

            if (lastEventAt < staleJobCutoff)
                stale++;
        }

        return stale;
    }

    private async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static HealthSignal BuildPolicySignal(AgentPolicy? policy)
    {
        if (policy?.IsPaused == true)
            return new HealthSignal("policy", "warning", "Automacao pausada", "Retome a politica do agente para buscar ou aplicar vagas.");

        var dailyLimit = policy?.MaxApplicationsPerDay ?? 50;
        var perRunLimit = policy?.MaxApplicationsPerRun ?? 10;
        return new HealthSignal("policy", "ok", $"Politica ativa ({perRunLimit}/execucao, {dailyLimit}/dia)", "");
    }

    private static HealthSignal BuildLimitSignal(ApplicationLimitSnapshot limit)
    {
        if (limit.DailyLimit <= 0)
            return new HealthSignal("limits", "ok", "Limite diario desativado", "A politica atual nao limita candidaturas por dia.");

        if (limit.IsReached)
            return new HealthSignal("limits", "warning", $"Limite diario atingido ({limit.AppliedToday}/{limit.DailyLimit})", "O piloto nao vai iniciar novas candidaturas hoje.");

        if (limit.RemainingToday <= Math.Max(1, limit.DailyLimit / 10))
            return new HealthSignal("limits", "warning", $"{limit.RemainingToday} candidatura(s) restante(s) hoje", $"Usadas {limit.AppliedToday} de {limit.DailyLimit}.");

        return new HealthSignal("limits", "ok", $"{limit.RemainingToday} candidatura(s) restante(s) hoje", $"Usadas {limit.AppliedToday} de {limit.DailyLimit}.");
    }

    private static HealthSignal BuildAiSignal(AiProvider? provider)
    {
        if (provider is null)
            return new HealthSignal("ai", "warning", "IA sem provedor padrao", "Configure um provedor em IA.");

        if (!provider.IsActive)
            return new HealthSignal("ai", "warning", $"IA padrao inativa: {provider.Name}", "Ative o provedor ou escolha outro padrao.");

        var label = provider.ProviderType.Equals("ollama", StringComparison.OrdinalIgnoreCase)
            ? $"IA local configurada: {provider.Model}"
            : $"IA configurada: {provider.Name}";
        return new HealthSignal("ai", "ok", label, provider.BaseUrl);
    }

    private static HealthSignal BuildExecutorSignal(string executionTarget, BrowserExtensionSession? session, DateTimeOffset liveCutoff)
    {
        if (executionTarget == ExecutionTargets.LocalDedicatedBrowser)
            return new HealthSignal("executor", "ok", "Navegador local dedicado", "O piloto usa um perfil separado do JobPilot.");

        if (executionTarget == ExecutionTargets.RemoteBrowser)
            return new HealthSignal("executor", "warning", "Worker remoto indisponivel", "Use extensao ou navegador local dedicado neste MVP.");

        if (executionTarget == ExecutionTargets.ServerConnector)
            return new HealthSignal("executor", "warning", "Conector server-side indisponivel", "Use extensao ou navegador local dedicado neste MVP.");

        if (session is null)
            return new HealthSignal("executor", "warning", "Extensao nao pareada", "Abra a extensao no Chrome e clique em conectar.");

        var connected = session.LastHeartbeatAt is not null && session.LastHeartbeatAt >= liveCutoff;
        if (!connected)
            return new HealthSignal("executor", "warning", "Extensao sem heartbeat", "Abra o Chrome ou clique em atualizar na extensao.");

        if (!IsVersionAtLeast(session.ExtensionVersion, RequiredExtensionVersion))
            return new HealthSignal("executor", "warning", $"Extensao desatualizada: {DisplayVersion(session.ExtensionVersion)}", $"Recarregue a versao {RequiredExtensionVersion} em chrome://extensions.");

        var page = session.IsSupportedPage ? "aba suportada" : "fora de pagina suportada";
        return new HealthSignal("executor", "ok", $"Extensao conectada ({page})", session.CurrentUrl);
    }

    private static HealthSignal BuildCommandSignal(string executionTarget, int pendingCommands, int staleCommands)
    {
        if (executionTarget != ExecutionTargets.Extension)
            return new HealthSignal("commands", "ok", "Fila da extensao inativa", "Executor atual nao depende da fila de comandos da extensao.");

        if (staleCommands > 0)
            return new HealthSignal("commands", "warning", $"{staleCommands} comando(s) travado(s)", "A extensao vai reenfileirar comandos antigos automaticamente.");

        if (pendingCommands > 0)
            return new HealthSignal("commands", "warning", $"{pendingCommands} comando(s) aguardando extensao", "Mantenha o Chrome aberto para executar a fila.");

        return new HealthSignal("commands", "ok", "Fila da extensao limpa", "");
    }

    private static HealthSignal BuildMaintenanceSignal(int staleAgentRuns, int staleApplicationJobs)
    {
        var staleTotal = staleAgentRuns + staleApplicationJobs;
        if (staleTotal == 0)
            return new HealthSignal("maintenance", "ok", "Sem trabalho preso", "");

        return new HealthSignal(
            "maintenance",
            "warning",
            $"{staleTotal} item(ns) preso(s)",
            "A manutencao automatica deve mover runs antigos para falha e candidaturas interrompidas para revisao.");
    }

    private static HealthSignal BuildQueueSignal(int queuedApplications, int openCheckpoints, int runningAgentRuns)
    {
        if (openCheckpoints > 0)
            return new HealthSignal("queue", "warning", $"{openCheckpoints} checkpoint(s) pendente(s)", "Resolva pendencias para liberar novas tentativas.");

        if (runningAgentRuns > 0)
            return new HealthSignal("queue", "ok", $"{runningAgentRuns} execucao(oes) em andamento", $"{queuedApplications} vaga(s) na fila.");

        if (queuedApplications > 0)
            return new HealthSignal("queue", "ok", $"{queuedApplications} vaga(s) prontas para aplicar", "");

        return new HealthSignal("queue", "idle", "Sem vagas na fila", "Execute uma busca ou revise filtros.");
    }

    private static string Rollup(IEnumerable<HealthSignal> signals)
    {
        var severities = signals.Select(x => x.Status).ToArray();
        if (severities.Contains("error")) return "error";
        if (severities.Contains("warning")) return "attention";
        if (severities.All(x => x == "idle")) return "idle";
        return "ok";
    }

    private static bool IsVersionAtLeast(string value, string minimum)
    {
        var currentParts = ParseVersion(value);
        var minimumParts = ParseVersion(minimum);
        for (var index = 0; index < Math.Max(currentParts.Length, minimumParts.Length); index++)
        {
            var current = index < currentParts.Length ? currentParts[index] : 0;
            var required = index < minimumParts.Length ? minimumParts[index] : 0;
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
}

public sealed record OperationalHealthResponse(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyCollection<HealthSignal> Signals,
    OperationalHealthMetrics Metrics);

public sealed record HealthSignal(string Key, string Status, string Label, string Detail);

public sealed record OperationalHealthMetrics(
    int QueuedApplications,
    int OpenCheckpoints,
    int RunningAgentRuns,
    int PendingExtensionCommands,
    int StaleExtensionCommands,
    int StaleAgentRuns,
    int StaleApplicationJobs,
    int DailyLimit,
    int AppliedToday,
    int RemainingToday);
