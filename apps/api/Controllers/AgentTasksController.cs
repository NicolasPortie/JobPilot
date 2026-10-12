using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia as tarefas do agente, incluindo fila de execução, descoberta de vagas e atualização de estado.
/// </summary>
[ApiController]
[Authorize(Policy = "ActiveAgent")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/agent")]
[EnableRateLimiting(RateLimitPolicies.Agent)]
public sealed class AgentTasksController(AgentTaskQueueService tasks) : ControllerBase
{
    /// <summary>
    /// Retorna uma tarefa do agente pelo identificador.
    /// </summary>
    /// <param name="id">Identificador da tarefa.</param>
    /// <returns>Tarefa encontrada.</returns>
    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<AgentTaskResponse>> GetTask(Guid id) =>
        this.ToActionResult(await tasks.GetTaskAsync(User.AgentUserId(), id));

    /// <summary>
    /// Retorna a próxima tarefa pendente do agente na fila.
    /// </summary>
    /// <returns>Próxima tarefa disponível.</returns>
    [HttpGet("tasks/next")]
    public async Task<ActionResult<AgentTaskResponse>> GetNextTask() =>
        this.ToActionResult(await tasks.GetNextTaskAsync(User.AgentUserId()));

    /// <summary>
    /// Salva as vagas descobertas pelo agente.
    /// </summary>
    /// <param name="request">Dados das vagas descobertas.</param>
    /// <returns>Resultado do processamento das vagas.</returns>
    [HttpPost("jobs/discovered")]
    public async Task<ActionResult<DiscoveredJobsResult>> SaveDiscoveredJobs(DiscoveredJobsRequest request) =>
        this.ToActionResult(await tasks.SaveDiscoveredJobsAsync(User.AgentUserId(), request));

    /// <summary>
    /// Executa o enfileiramento automático de tarefas do agente.
    /// </summary>
    /// <returns>Resultado do enfileiramento automático.</returns>
    [HttpPost("auto-queue")]
    public async Task<ActionResult<AutoQueueResult>> AutoQueue() =>
        this.ToActionResult(await tasks.AutoQueueAsync(User.AgentUserId()));

    /// <summary>
    /// Atualiza o estado de uma tarefa do agente.
    /// </summary>
    /// <param name="id">Identificador da tarefa.</param>
    /// <param name="request">Dados do novo estado.</param>
    /// <returns>Tarefa atualizada.</returns>
    [HttpPatch("tasks/{id:guid}/state")]
    public async Task<ActionResult<ApplicationJobResponse>> UpdateState(Guid id, AgentStateRequest request) =>
        this.ToActionResult(await tasks.UpdateStateAsync(User.AgentUserId(), id, request));

    /// <summary>
    /// Atualiza a superfície de candidatura de uma tarefa.
    /// </summary>
    /// <param name="id">Identificador da tarefa.</param>
    /// <param name="request">Dados da superfície de candidatura.</param>
    /// <returns>Lead de vaga atualizado.</returns>
    [HttpPatch("tasks/{id:guid}/apply-surface")]
    public async Task<ActionResult<JobLeadResponse>> UpdateApplySurface(Guid id, ApplySurfaceRequest request) =>
        this.ToActionResult(await tasks.UpdateApplySurfaceAsync(User.AgentUserId(), id, request));
}
