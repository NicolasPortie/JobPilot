using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Infrastructure;

public sealed class JobPilotApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobPilotApiClient(string apiBaseUrl, string token)
    {
        _http = new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ── Discovery ──
    public async Task PostDiscoveredAsync(IEnumerable<DiscoveredJobDto> jobs)
    {
        var response = await _http.PostAsJsonAsync("api/agent/jobs/discovered", new { jobs }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JobFilter?> GetJobFilterAsync()
    {
        var response = await _http.GetAsync("api/agent/job-filters");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JobFilter>(JsonOptions);
    }

    // ── Tasks / Queue ──
    public async Task<AgentTaskResponse?> GetNextTaskAsync()
    {
        var response = await _http.GetAsync("api/agent/tasks/next");
        Console.WriteLine($"[DEBUG] GetNextTaskAsync status: {response.StatusCode}, base: {_http.BaseAddress}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentTaskResponse>(JsonOptions);
    }

    public async Task<AgentTaskResponse?> GetTaskAsync(Guid taskId)
    {
        var response = await _http.GetAsync($"api/agent/tasks/{taskId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentTaskResponse>(JsonOptions);
    }

    public async Task<ExtensionStatusResponse?> GetExtensionStatusAsync()
    {
        var response = await _http.GetAsync("api/extension/status");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ExtensionStatusResponse>(JsonOptions);
    }

    public async Task<ExtensionCommandResponse?> QueueExtensionCommandAsync(QueueExtensionCommandRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/extension/commands", request, JsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ExtensionCommandResponse>(JsonOptions);
    }

    public async Task<ExtensionCommandStatusResponse?> GetExtensionCommandAsync(Guid commandId)
    {
        var response = await _http.GetAsync($"api/extension/commands/{commandId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ExtensionCommandStatusResponse>(JsonOptions);
    }

    public async Task SetStateAsync(Guid taskId, string state, string? reason = null)
    {
        var response = await _http.PatchAsJsonAsync($"api/agent/tasks/{taskId}/state", new { state, reason }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    // ── Profile ──
    public async Task SetApplySurfaceAsync(Guid taskId, ApplySurfaceRequest request)
    {
        var response = await _http.PatchAsJsonAsync($"api/agent/tasks/{taskId}/apply-surface", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ProfileValidationResult?> ValidateProfileAsync()
    {
        var response = await _http.GetAsync("api/agent/profile/validate");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ProfileValidationResult>(JsonOptions);
    }

    // ── Platform Credentials ──
    public async Task<List<PlatformCredential>?> GetPlatformCredentialsAsync()
    {
        var response = await _http.GetAsync("api/agent/platform-credentials");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<PlatformCredential>>(JsonOptions);
    }

    // ── LLM Decision Endpoints ──
    public async Task<BrowserActionPlanResponse?> PlanBrowserActionAsync(BrowserActionPlanRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/browser-action-plan", request, JsonOptions);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<BrowserActionPlanResponse>(JsonOptions);
        }
        catch { return null; }
    }

    public async Task<PageStateResponse?> ClassifyPageStateAsync(PageStateRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/page-state", request, JsonOptions);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PageStateResponse>(JsonOptions);
        }
        catch { return null; }
    }

    public async Task<StepValidationResponse?> ValidateStepAsync(StepValidationRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/step-validation", request, JsonOptions);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<StepValidationResponse>(JsonOptions);
        }
        catch { return null; }
    }

    // ── Answers / Patterns ──
    public async Task<ResolvedAnswer?> ResolveAnswerAsync(string questionText, string? jobContext)
    {
        var response = await _http.PostAsJsonAsync("api/question-patterns/resolve", new { questionText, jobContext }, JsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ResolvedAnswer>(JsonOptions);
    }

    public async Task<string?> DraftAnswerAsync(string questionText, string? jobContext)
    {
        var response = await _http.PostAsJsonAsync("api/agent/answer-draft", new { questionText, jobContext }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var draft = await response.Content.ReadFromJsonAsync<AnswerDraftResponse>(JsonOptions);
        return draft?.Text;
    }

    // ── Telemetry / Events ──
    public async Task EventAsync(Guid? taskId, string eventType, string detail)
    {
        var response = await _http.PostAsJsonAsync("api/agent/events", new { applicationJobId = taskId, eventType, detail }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid?> StartRunAsync(Guid? taskId, string agentName, string mode, string policyMode)
    {
        var response = await _http.PostAsJsonAsync("api/agent/runs", new AgentRunRequest(taskId, agentName, mode, policyMode), JsonOptions);
        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<AgentRunResponse>(JsonOptions);
        return run?.Id;
    }

    public async Task CompleteRunAsync(Guid? runId, string state)
    {
        if (runId is null) return;
        var response = await _http.PatchAsJsonAsync($"api/agent/runs/{runId}", new AgentRunCompleteRequest(state), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task StepAsync(AgentStepRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/agent/steps", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid?> TraceStepAsync(AgentTraceStepRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/agent/trace-steps", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var trace = await response.Content.ReadFromJsonAsync<AgentTraceStepResponse>(JsonOptions);
        return trace?.Id;
    }

    public async Task ApplicationFieldAsync(ApplicationFieldRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/agent/application-fields", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplicationAnswerAsync(ApplicationAnswerRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/agent/application-answers", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task WorkflowMemoryAsync(SiteWorkflowMemoryRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/agent/site-workflow-memory", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    // ── Checkpoints ──
    public async Task<List<AgentLearning>> GetActiveLearningsAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/agent/learnings/active");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<AgentLearning>>(JsonOptions) ?? [];
        }
        catch { return []; }
    }

    public async Task<AgentLearning?> LearningCandidateAsync(AgentLearningRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/learnings", request, JsonOptions);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentLearning>(JsonOptions);
        }
        catch { return null; }
    }

    public async Task LearningOutcomeAsync(AgentLearningOutcomeRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/learnings/outcome", request, JsonOptions);
            if (!response.IsSuccessStatusCode) return;
        }
        catch { }
    }

    public async Task GenerateLearningsFromJobAsync(Guid applicationJobId)
    {
        try
        {
            var response = await _http.PostAsync($"api/agent/learnings/generate-from-job/{applicationJobId}", null);
            if (!response.IsSuccessStatusCode) return;
        }
        catch { }
    }

    public async Task CheckpointAsync(
        Guid? taskId,
        string reasonCode,
        string stage,
        string fieldLabel,
        string detail,
        Guid? agentRunId = null,
        Guid? traceStepId = null,
        string suggestedUserAction = "",
        string resumeMode = "reobserve")
    {
        var response = await _http.PostAsJsonAsync("api/agent/checkpoints", new AgentCheckpointRequest(
            taskId,
            agentRunId,
            traceStepId,
            reasonCode,
            stage,
            fieldLabel,
            detail,
            suggestedUserAction,
            resumeMode), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    // ── Policy ──
    public async Task<AgentPolicy?> GetPolicyAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/agent/policy");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentPolicy>(JsonOptions);
        }
        catch { return null; }
    }

    // ── Patterns & Answers (batch download for local resolver) ──
    public async Task<List<QuestionPattern>> GetQuestionPatternsAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/question-patterns");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<QuestionPattern>>(JsonOptions) ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ReusableAnswer>> GetReusableAnswersAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/answers");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<ReusableAnswer>>(JsonOptions) ?? [];
        }
        catch { return []; }
    }

    public async Task<AutoQueueResponse?> AutoQueueAsync()
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/agent/auto-queue", new {}, JsonOptions);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AutoQueueResponse>(JsonOptions);
        }
        catch { return null; }
    }
}

public sealed record AutoQueueResponse(int Queued, int Total);
