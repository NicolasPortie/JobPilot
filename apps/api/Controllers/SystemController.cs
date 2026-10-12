using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Provides basic system metadata for operators.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/system")]
public sealed class SystemController(ReadinessService readiness) : ControllerBase
{
    /// <summary>
    /// Returns basic application metadata.
    /// </summary>
    /// <returns>Application metadata.</returns>
    [HttpGet("status")]
    public IActionResult Status() =>
        Ok(new { status = "ok", app = "JobPilot", timestamp = DateTimeOffset.UtcNow });

    /// <summary>
    /// Checks whether application dependencies are ready.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readiness status or 503.</returns>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var ready = await readiness.CanConnectAsync(cancellationToken);
        return ready
            ? Ok(new { status = "ready", database = "connected", timestamp = DateTimeOffset.UtcNow })
            : Problem("Database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
