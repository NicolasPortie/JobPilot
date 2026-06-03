using System.Text.Json;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Infrastructure;

/// <summary>
/// Cliente LLM que delega ao backend do JobPilot os prompts estruturados
/// para decisão de ações de browser, classificação de página e validação de steps.
/// </summary>
public sealed class LlmClient
{
    private readonly JobPilotApiClient _client;
    private readonly bool _enabled;

    public LlmClient(JobPilotApiClient client, bool enabled = true)
    {
        _client = client;
        _enabled = enabled;
    }

    public bool Enabled => _enabled;

    /// <summary>
    /// Consulta o LLM (via backend) para decidir a próxima ação no browser.
    /// Retorna null se desabilitado, se o backend falhar, ou se o LLM não retornar JSON válido.
    /// </summary>
    public async Task<BrowserActionPlanResponse?> PlanBrowserActionAsync(
        PageObservation observation,
        string jobContext,
        string pageState,
        string objective = "Fill the job application form and submit it.",
        Guid? applicationJobId = null,
        int attempt = 1,
        string? previousFailure = null,
        string? decisionMemory = null)
    {
        if (!_enabled) return null;

        var observationJson = JsonSerializer.Serialize(observation, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var request = new BrowserActionPlanRequest(objective, pageState, observationJson, jobContext, applicationJobId, attempt, previousFailure, decisionMemory);
        return await _client.PlanBrowserActionAsync(request);
    }

    /// <summary>
    /// Classifica o estado da página via LLM (login, captcha, form, confirmation, etc.)
    /// </summary>
    public async Task<PageStateResponse?> ClassifyPageStateAsync(PageObservation observation)
    {
        if (!_enabled) return null;

        var request = new PageStateRequest(observation.Url, observation.Title, observation.VisibleText);
        return await _client.ClassifyPageStateAsync(request);
    }

    /// <summary>
    /// Valida se uma ação executada realmente mudou o estado da página.
    /// </summary>
    public async Task<StepValidationResponse?> ValidateStepAsync(
        BrowserAction action,
        PageObservation before,
        PageObservation after,
        string? jobContext = null)
    {
        if (!_enabled) return null;

        var actionJson = JsonSerializer.Serialize(action, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        var beforeJson = JsonSerializer.Serialize(before, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        var afterJson = JsonSerializer.Serialize(after, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var request = new StepValidationRequest(actionJson, beforeJson, afterJson, jobContext);
        return await _client.ValidateStepAsync(request);
    }
}
