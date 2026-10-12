using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Mantém os endpoints legados de automação fora do fluxo principal do produto.
///
/// O MVP atual não inicia navegador, extensão ou processo de candidatura a partir
/// da aplicação web. Os contratos permanecem temporariamente para permitir uma
/// migração segura de instalações antigas, mas as ações HTTP estão desativadas.
/// </summary>
[ApiController]
[Route("api/automation")]
public sealed class AutomationController : ControllerBase
{
    /// <summary>
    /// Informa que a descoberta automatizada foi desativada no MVP.
    /// </summary>
    /// <returns>Resposta HTTP 410.</returns>
    [AllowAnonymous]
    [HttpPost("discover")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Discover() => LegacyAutomationDisabled();

    /// <summary>
    /// Informa que a candidatura automatizada foi desativada no MVP.
    /// </summary>
    /// <returns>Resposta HTTP 410.</returns>
    [AllowAnonymous]
    [HttpPost("apply")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Apply() => LegacyAutomationDisabled();

    private ObjectResult LegacyAutomationDisabled() => Problem(
        statusCode: StatusCodes.Status410Gone,
        title: "Automação desativada",
        detail: "O JobPilot não inicia navegador nem envia candidaturas automaticamente no MVP. Use POST /api/job-targets/organize (link/texto) ou POST /api/job-targets/organize-screenshot (imagem/PDF) e candidate-se manualmente.");
}
