using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia as execuções (runs) de candidaturas do usuário.
/// </summary>
[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/runs")]
public sealed class RunsController(ApplicationRunService runs) : ControllerBase
{
    /// <summary>
    /// Lista as execuções de candidatura do usuário.
    /// </summary>
    /// <returns>Lista de execuções.</returns>
    [HttpGet]
    public async Task<ActionResult<List<ApplicationRunResponse>>> List() =>
        Ok(await runs.ListAsync(User.UserId()));

    /// <summary>
    /// Cria uma nova execução de candidatura.
    /// </summary>
    /// <param name="request">Dados da execução a ser criada.</param>
    /// <returns>Execução criada com status 201.</returns>
    [HttpPost]
    public async Task<ActionResult<ApplicationRunCreationResult>> Create(CreateRunRequest request)
    {
        var result = await runs.CreateAsync(User.UserId(), request);
        return this.ToActionResult(result, $"/api/runs/{result.Value?.Run.Id}");
    }
}
