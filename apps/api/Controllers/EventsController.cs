using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Consulta os eventos de execução recentes do sistema.
/// </summary>
[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/events")]
public sealed class EventsController(ExecutionEventQueryService events) : ControllerBase
{
    /// <summary>
    /// Lista os eventos de execução mais recentes.
    /// </summary>
    /// <param name="limit">Quantidade máxima de eventos a retornar.</param>
    /// <returns>Lista de eventos recentes.</returns>
    [HttpGet]
    public async Task<ActionResult<List<ExecutionEventResponse>>> List([FromQuery] int limit = 100) =>
        Ok(await events.ListRecentAsync(User.UserId(), limit));
}
