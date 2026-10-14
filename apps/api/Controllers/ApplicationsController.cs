using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Fluxo rápido de candidaturas: gera o currículo adaptado a partir de uma vaga e mantém o histórico.
/// O envio da candidatura é sempre manual.
/// </summary>
[ApiController]
[Authorize]
[Route("api/applications")]
public sealed class ApplicationsController(QuickApplicationService applications) : ControllerBase
{
    /// <summary>Organiza a vaga (link, vários links ou texto) e gera o currículo adaptado em uma chamada.</summary>
    [HttpPost("quick")]
    [EnableRateLimiting(RateLimitPolicies.Analysis)]
    public async Task<ActionResult<QuickTailorResponse>> Quick(QuickTailorRequest request) =>
        Ok(await applications.TailorAsync(User.UserId(), request));

    /// <summary>Currículos já adaptados para vagas, com o estado de candidatura manual.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApplicationHistoryItem[]>> History() =>
        Ok(await applications.HistoryAsync(User.UserId()));

    /// <summary>Marca (ou desmarca) que o usuário já enviou a candidatura para a vaga deste currículo.</summary>
    [HttpPut("{resumeVersionId:guid}/applied")]
    public async Task<IActionResult> SetApplied(Guid resumeVersionId, SetApplicationStatusRequest request)
    {
        await applications.SetAppliedAsync(User.UserId(), resumeVersionId, request.Applied);
        return NoContent();
    }
}
