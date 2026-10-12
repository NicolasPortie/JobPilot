using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AutomationReadinessService(JobPilotDbContext db)
{
    private readonly JobPilotDbContext _db = db;

    public async Task<ServiceResult<AutomationOperationResult>?> ValidateExtensionReadyAsync(Guid userId, string action)
    {
        // A extensão do navegador foi removida do produto: este gate sempre reprova.
        const string message = "A extensão do navegador foi removida do produto. Selecione o navegador local dedicado na política do agente antes de prosseguir.";
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.extension.removed",
            Detail = $"{message} action={action}"
        });
        await _db.SaveChangesAsync();
        return ServiceResult<AutomationOperationResult>.Invalid(message);
    }

    public async Task<ServiceResult<AutomationOperationResult>?> ValidatePolicyActiveAsync(Guid userId, AgentPolicy? policy, string action)
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
        return ServiceResult<AutomationOperationResult>.Ok(
            new AutomationOperationResult(
                false,
                action.Contains("buscar", StringComparison.OrdinalIgnoreCase) ? "discover" : "apply",
                Message: message,
                Paused: true));
    }

    public async Task<ServiceResult<AutomationOperationResult>?> ValidatePlatformCredentialReadyAsync(Guid userId, string platform, string action)
    {
        var hasCredential = await _db.PlatformCredentials.AnyAsync(x =>
            x.UserId == userId &&
            x.IsActive &&
            x.Platform.ToLower() == platform.ToLower() &&
            x.EncryptedPassword != "");

        if (hasCredential)
            return null;

        var message = $"Salve a credencial do {DisplayPlatform(platform)} em Configuracoes antes de {action} com o navegador local dedicado. Ele roda em segundo plano (sem abrir janela) e precisa da credencial para logar.";
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "automation.credential.missing",
            Detail = $"{message} action={action}; platform={platform}"
        });
        await _db.SaveChangesAsync();
        return ServiceResult<AutomationOperationResult>.Invalid(
            message,
            new AutomationOperationResult(false, "", ExecutionTargets.LocalDedicatedBrowser, Message: message));
    }

    private static string DisplayPlatform(string platform) =>
        string.Equals(platform, "linkedin", StringComparison.OrdinalIgnoreCase) ? "LinkedIn" : platform;

    public ServiceResult<AutomationOperationResult>? ValidateExecutionTarget(Guid userId, string executionTarget, string action)
    {
        if (ExecutionTargets.IsImplemented(executionTarget))
            return null;

        var message = executionTarget switch
        {
            ExecutionTargets.RemoteBrowser => "O worker remoto ainda nao esta habilitado neste MVP local. Use o navegador local dedicado.",
            ExecutionTargets.ServerConnector => "Conectores server-side ainda estao em beta fechado. Use o navegador local dedicado.",
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
        return ServiceResult<AutomationOperationResult>.Invalid(
            message,
            new AutomationOperationResult(false, "", executionTarget, Message: message));
    }

}

