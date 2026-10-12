using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Coordena a execução do agente automatizado, incluindo rascunhos de respostas,
/// validação de perfil, classificação de páginas, execução de runs e gerenciamento de aprendizados.
/// </summary>
[ApiController]
[Authorize(Policy = "ActiveAgent")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/agent")]
[EnableRateLimiting(RateLimitPolicies.Agent)]
public sealed class AgentController(
    IAgentProtocolService protocol,
    IAiChatService ai,
    JobFilterService filters,
    ProfileGuard profileGuard,
    AgentAnswerResolutionService answerResolution,
    AgentLearningService learnings) : ControllerBase
{
    /// <summary>
    /// Gera um rascunho de resposta para uma pergunta da candidatura.
    /// </summary>
    /// <param name="request">Dados da pergunta e contexto para geração do rascunho.</param>
    /// <returns>Rascunho de resposta gerado pela IA.</returns>
    [HttpPost("answer-draft")]
    public Task<AnswerDraftResponse> DraftAnswer(AnswerDraftRequest request) => protocol.DraftAnswerAsync(User.AgentUserId(), request);

    /// <summary>
    /// Retorna os filtros de vagas configurados para o agente do usuário.
    /// </summary>
    /// <returns>Filtros de vagas do agente.</returns>
    [HttpGet("job-filters")]
    public async Task<JobFilterResponse> GetJobFilters() =>
        JobFilterResponse.From(await filters.GetOrCreateAsync(User.AgentUserId()));

    /// <summary>
    /// Valida o perfil do candidato e retorna possíveis problemas encontrados.
    /// </summary>
    /// <returns>Resultado da validação do perfil com lista de issues.</returns>
    [HttpGet("profile/validate")]
    public async Task<ProfileValidationResponse> ValidateProfile()
    {
        var issues = await profileGuard.ValidateAsync(User.AgentUserId());
        return new(issues.All(x => !x.IsCritical), issues.Any(x => x.IsCritical), issues);
    }

    /// <summary>
    /// Classifica o estado atual de uma página de candidatura.
    /// </summary>
    /// <param name="request">Dados da página para classificação.</param>
    /// <returns>Classificação do estado da página.</returns>
    [HttpPost("page-state")]
    public Task<PageStateResponse> ClassifyPageState(PageStateRequest request) =>
        ai.ClassifyPageStateAsync(User.AgentUserId(), request);

    /// <summary>
    /// Valida se um passo da candidatura foi executado corretamente.
    /// </summary>
    /// <param name="request">Dados do passo para validação.</param>
    /// <returns>Resultado da validação do passo.</returns>
    [HttpPost("step-validation")]
    public Task<StepValidationResponse> ValidateStep(StepValidationRequest request) =>
        ai.ValidateApplicationStepAsync(User.AgentUserId(), request);

    /// <summary>
    /// Gera um plano de ações do navegador para executar na página atual.
    /// </summary>
    /// <param name="request">Contexto da página e objetivo para o plano de ações.</param>
    /// <returns>Plano de ações do navegador.</returns>
    [HttpPost("browser-action-plan")]
    public Task<BrowserActionPlanResponse> PlanBrowserAction(BrowserActionPlanRequest request) =>
        protocol.PlanBrowserActionAsync(User.AgentUserId(), request);

    /// <summary>
    /// Resolve uma resposta do agente com base no contexto da pergunta.
    /// </summary>
    /// <param name="request">Dados da pergunta para resolução da resposta.</param>
    /// <returns>Resposta resolvida para a pergunta.</returns>
    [HttpPost("resolve-answer")]
    public Task<ResolveAgentAnswerResponse> ResolveAnswer(ResolveAgentAnswerRequest request) =>
        answerResolution.ResolveAsync(User.AgentUserId(), request);

    /// <summary>
    /// Inicia uma nova execução (run) do agente.
    /// </summary>
    /// <param name="request">Configuração da execução do agente.</param>
    /// <returns>Run criada com status 201.</returns>
    [HttpPost("runs")]
    public async Task<ActionResult<AgentRunResponse>> StartRun(AgentRunRequest request)
    {
        var run = await protocol.StartRunAsync(User.AgentUserId(), request);
        return Created($"/api/agent/runs/{run.Id}", AgentRunResponse.From(run));
    }

    /// <summary>
    /// Completa uma execução (run) do agente.
    /// </summary>
    /// <param name="id">Identificador da run.</param>
    /// <param name="request">Dados de conclusão da run.</param>
    /// <returns>Run atualizada.</returns>
    [HttpPatch("runs/{id:guid}")]
    public async Task<ActionResult<AgentRunResponse>> CompleteRun(Guid id, AgentRunCompleteRequest request) =>
        this.ToActionResult((await protocol.CompleteRunAsync(User.AgentUserId(), id, request)).Map(AgentRunResponse.From));

    /// <summary>
    /// Salva um passo executado pelo agente.
    /// </summary>
    /// <param name="request">Dados do passo executado.</param>
    /// <returns>Status 202 indicando aceitação.</returns>
    [HttpPost("steps")]
    public async Task<IActionResult> SaveStep(AgentStepRequest request)
    {
        await protocol.SaveStepAsync(User.AgentUserId(), request);
        return Accepted();
    }

    /// <summary>
    /// Salva um passo de trace da execução do agente.
    /// </summary>
    /// <param name="request">Dados do passo de trace.</param>
    /// <returns>Resposta com o identificador do trace criado.</returns>
    [HttpPost("trace-steps")]
    public async Task<ActionResult<AgentTraceStepResponse>> SaveTraceStep(AgentTraceStepRequest request)
    {
        var id = await protocol.SaveTraceStepAsync(User.AgentUserId(), request);
        return Created($"/api/agent/trace-steps/{id}", new AgentTraceStepResponse(id));
    }

    /// <summary>
    /// Salva um campo preenchido durante a candidatura.
    /// </summary>
    /// <param name="request">Dados do campo da candidatura.</param>
    /// <returns>Status 202 indicando aceitação.</returns>
    [HttpPost("application-fields")]
    public async Task<IActionResult> SaveApplicationField(ApplicationFieldRequest request)
    {
        await protocol.SaveApplicationFieldAsync(User.AgentUserId(), request);
        return Accepted();
    }

    /// <summary>
    /// Salva uma resposta fornecida durante a candidatura.
    /// </summary>
    /// <param name="request">Dados da resposta da candidatura.</param>
    /// <returns>Status 202 indicando aceitação.</returns>
    [HttpPost("application-answers")]
    public async Task<IActionResult> SaveApplicationAnswer(ApplicationAnswerRequest request)
    {
        await protocol.SaveApplicationAnswerAsync(User.AgentUserId(), request);
        return Accepted();
    }

    /// <summary>
    /// Salva a memória de workflow de um site após a candidatura.
    /// </summary>
    /// <param name="request">Dados da memória de workflow do site.</param>
    /// <returns>Status 202 indicando aceitação.</returns>
    [HttpPost("site-workflow-memory")]
    public async Task<IActionResult> SaveWorkflowMemory(SiteWorkflowMemoryRequest request)
    {
        await protocol.SaveWorkflowMemoryAsync(User.AgentUserId(), request);
        return Accepted();
    }

    /// <summary>
    /// Lista os aprendizados do agente com filtros opcionais.
    /// </summary>
    /// <param name="status">Filtro por status do aprendizado.</param>
    /// <param name="type">Filtro por tipo do aprendizado.</param>
    /// <param name="risk">Filtro por nível de risco.</param>
    /// <param name="domain">Filtro por domínio.</param>
    /// <returns>Lista de aprendizados filtrados.</returns>
    [HttpGet("learnings")]
    public async Task<List<AgentLearningResponse>> ListLearnings(string? status, string? type, string? risk, string? domain) =>
        (await learnings.ListAsync(User.AgentUserId(), status, type, risk, domain)).Select(AgentLearningResponse.From).ToList();

    /// <summary>
    /// Retorna os aprendizados ativos do agente.
    /// </summary>
    /// <returns>Lista de aprendizados ativos.</returns>
    [HttpGet("learnings/active")]
    public async Task<List<AgentLearningResponse>> GetActiveLearnings() =>
        (await learnings.ActiveAsync(User.AgentUserId())).Select(AgentLearningResponse.From).ToList();

    /// <summary>
    /// Retorna um aprendizado específico pelo identificador.
    /// </summary>
    /// <param name="id">Identificador do aprendizado.</param>
    /// <returns>Aprendizado encontrado ou 404.</returns>
    [HttpGet("learnings/{id:guid}")]
    public async Task<ActionResult<AgentLearningResponse>> GetLearning(Guid id) =>
        await learnings.GetAsync(User.AgentUserId(), id) is { } learning ? Ok(AgentLearningResponse.From(learning)) : NotFound();

    /// <summary>
    /// Salva um novo aprendizado do agente.
    /// </summary>
    /// <param name="request">Dados do aprendizado.</param>
    /// <returns>Aprendizado salvo.</returns>
    [HttpPost("learnings")]
    public async Task<AgentLearningResponse> SaveLearning(AgentLearningRequest request) =>
        AgentLearningResponse.From(await learnings.UpsertCandidateAsync(User.AgentUserId(), request));

    /// <summary>
    /// Aprova um aprendizado pendente.
    /// </summary>
    /// <param name="id">Identificador do aprendizado.</param>
    /// <param name="request">Dados da revisão.</param>
    /// <returns>Aprendizado atualizado.</returns>
    [HttpPost("learnings/{id:guid}/approve")]
    public Task<ActionResult<AgentLearningResponse>> ApproveLearning(Guid id, AgentLearningReviewRequest request) => ReviewLearning(id, "approve");

    /// <summary>
    /// Rejeita um aprendizado pendente.
    /// </summary>
    /// <param name="id">Identificador do aprendizado.</param>
    /// <param name="request">Dados da revisão.</param>
    /// <returns>Aprendizado atualizado.</returns>
    [HttpPost("learnings/{id:guid}/reject")]
    public Task<ActionResult<AgentLearningResponse>> RejectLearning(Guid id, AgentLearningReviewRequest request) => ReviewLearning(id, "reject");

    /// <summary>
    /// Desativa um aprendizado.
    /// </summary>
    /// <param name="id">Identificador do aprendizado.</param>
    /// <param name="request">Dados da revisão.</param>
    /// <returns>Aprendizado atualizado.</returns>
    [HttpPost("learnings/{id:guid}/disable")]
    public Task<ActionResult<AgentLearningResponse>> DisableLearning(Guid id, AgentLearningReviewRequest request) => ReviewLearning(id, "disable");

    /// <summary>
    /// Gera aprendizados a partir de uma candidatura existente.
    /// </summary>
    /// <param name="applicationJobId">Identificador da candidatura.</param>
    /// <returns>Lista de aprendizados gerados.</returns>
    [HttpPost("learnings/generate-from-job/{applicationJobId:guid}")]
    public async Task<List<AgentLearningResponse>> GenerateLearnings(Guid applicationJobId) =>
        (await learnings.GenerateFromJobAsync(User.AgentUserId(), applicationJobId)).Select(AgentLearningResponse.From).ToList();

    /// <summary>
    /// Registra o resultado de um aprendizado aplicado.
    /// </summary>
    /// <param name="request">Dados do resultado do aprendizado.</param>
    /// <returns>Aprendizado atualizado ou 404.</returns>
    [HttpPost("learnings/outcome")]
    public async Task<ActionResult<AgentLearningResponse>> RecordLearningOutcome(AgentLearningOutcomeRequest request) =>
        await learnings.RecordOutcomeAsync(User.AgentUserId(), request) is { } learning ? Ok(AgentLearningResponse.From(learning)) : NotFound();

    /// <summary>
    /// Salva um evento de execução do agente.
    /// </summary>
    /// <param name="input">Dados do evento de execução.</param>
    /// <returns>Status 202 indicando aceitação.</returns>
    [HttpPost("events")]
    public async Task<IActionResult> SaveEvent(ExecutionEventRequest input)
    {
        await protocol.SaveEventAsync(User.AgentUserId(), input);
        return Accepted();
    }

    /// <summary>
    /// Cria um checkpoint durante a execução do agente.
    /// </summary>
    /// <param name="request">Dados do checkpoint.</param>
    /// <returns>Checkpoint criado com status 201.</returns>
    [HttpPost("checkpoints")]
    public async Task<ActionResult<CheckpointResponse>> CreateCheckpoint(AgentCheckpointRequest request)
    {
        var checkpoint = await protocol.CreateCheckpointAsync(User.AgentUserId(), request);
        return Created($"/api/checkpoints/{checkpoint.Id}", CheckpointResponse.From(checkpoint));
    }

    private async Task<ActionResult<AgentLearningResponse>> ReviewLearning(Guid id, string decision) =>
        await learnings.ReviewAsync(User.AgentUserId(), User.AgentUserId(), id, decision) is { } learning ? Ok(AgentLearningResponse.From(learning)) : NotFound();
}
