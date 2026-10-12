using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/platform-credentials")]
public sealed class PlatformCredentialsController : ControllerBase
{
    [HttpGet]
    public IActionResult List() => LegacyRuntimeDisabled();

    [HttpPost]
    public IActionResult Create() => LegacyRuntimeDisabled();

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id) => LegacyRuntimeDisabled();

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id) => LegacyRuntimeDisabled();

    private IActionResult LegacyRuntimeDisabled() => Problem(
        statusCode: StatusCodes.Status410Gone,
        title: "Runtime legado desativado",
        detail: "O JobPilot não armazena credenciais de plataformas nem controla navegadores no produto atual.");
}
