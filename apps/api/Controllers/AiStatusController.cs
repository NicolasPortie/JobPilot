using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Informa se a IA usada na análise de vagas e na adaptação está configurada e respondendo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/ai/status")]
public sealed class AiStatusController(AiCompletionService completion) : ControllerBase
{
    /// <summary>
    /// Retorna provedor, modelo e o resultado da última chamada, sem expor chaves.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AiStatusResponse>> Get() =>
        Ok(await completion.GetStatusAsync(User.UserId()));
}
