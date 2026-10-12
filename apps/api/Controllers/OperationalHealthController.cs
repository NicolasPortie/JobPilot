using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Consulta a saúde operacional e permite a recuperação de trabalhos travados.
/// </summary>
[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/health/operational")]
public sealed class OperationalHealthController(OperationalHealthService health, ApplicationMaintenanceService maintenance) : ControllerBase
{
    /// <summary>
    /// Retorna o estado de saúde operacional do usuário.
    /// </summary>
    /// <returns>Resumo da saúde operacional.</returns>
    [HttpGet]
    public async Task<ActionResult<OperationalHealthResponse>> Get() => Ok(await health.GetAsync(User.UserId()));

    /// <summary>
    /// Recupera trabalhos travados em execução.
    /// </summary>
    /// <returns>Resultado da recuperação.</returns>
    [HttpPost("recover")]
    public async Task<ActionResult<ApplicationMaintenanceResult>> Recover() =>
        Ok(await maintenance.RecoverStaleWorkAsync(DateTimeOffset.UtcNow, User.UserId()));
}
