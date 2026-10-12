using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia os checkpoints abertos e suas resoluções durante as execuções do agente.
/// </summary>
[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/checkpoints")]
public sealed class CheckpointsController(
    CheckpointQueryService checkpoints,
    CheckpointResolutionService resolution) : ControllerBase
{
    /// <summary>
    /// Lista os checkpoints em aberto do usuário.
    /// </summary>
    /// <returns>Lista de checkpoints abertos.</returns>
    [HttpGet]
    public async Task<ActionResult<List<CheckpointResponse>>> ListOpen() =>
        Ok(await checkpoints.ListOpenAsync(User.UserId()));

    /// <summary>
    /// Resolve um checkpoint específico.
    /// </summary>
    /// <param name="id">Identificador do checkpoint.</param>
    /// <param name="request">Dados da resolução.</param>
    /// <returns>Checkpoint resolvido.</returns>
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<CheckpointResponse>> Resolve(Guid id, ResolveCheckpointRequest request) =>
        this.ToActionResult(await resolution.ResolveAsync(User.UserId(), id, request));
}
