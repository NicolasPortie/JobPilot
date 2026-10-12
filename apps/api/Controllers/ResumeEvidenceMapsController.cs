using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia os mapas de evidências de currículo para alinhamento com requisitos de vagas.
/// </summary>
[ApiController]
[Authorize]
[Route("api/resume/evidence-maps")]
public sealed class ResumeEvidenceMapsController(ResumeEvidenceMapService evidenceMaps) : ControllerBase
{
    /// <summary>
    /// Constrói um novo mapa de evidências de currículo.
    /// </summary>
    /// <param name="request">Dados para construção do mapa.</param>
    /// <returns>Mapa de evidências criado.</returns>
    [HttpPost]
    public async Task<ActionResult<ResumeEvidenceMapDetail>> Build(BuildResumeEvidenceMapRequest request) =>
        Ok(await evidenceMaps.BuildAsync(User.UserId(), request));

    /// <summary>
    /// Retorna um mapa de evidências pelo identificador.
    /// </summary>
    /// <param name="id">Identificador do mapa.</param>
    /// <returns>Mapa encontrado ou 404.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResumeEvidenceMapDetail>> Get(Guid id)
    {
        var evidenceMap = await evidenceMaps.GetAsync(User.UserId(), id);
        return evidenceMap is null ? NotFound() : Ok(evidenceMap);
    }
}
