using JobPilot.Api.Application;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/application-jobs/{id:guid}")]
public sealed class ApplicationJobsAuditController(ApplicationAuditService audit) : ControllerBase
{
    [HttpGet("fields")]
    public async Task<IActionResult> GetFields(Guid id) => Ok(await audit.GetFieldsAsync(User.UserId(), id));

    [HttpGet("answers")]
    public async Task<IActionResult> GetAnswers(Guid id) => Ok(await audit.GetAnswersAsync(User.UserId(), id));

    [HttpGet("steps")]
    public async Task<IActionResult> GetSteps(Guid id) => Ok(await audit.GetStepsAsync(User.UserId(), id));

    [HttpGet("trace-steps")]
    public async Task<IActionResult> GetTraceSteps(Guid id) => Ok(await audit.GetTraceStepsAsync(User.UserId(), id));

    [HttpGet("checkpoints")]
    public async Task<IActionResult> GetCheckpoints(Guid id) => Ok(await audit.GetCheckpointsAsync(User.UserId(), id));

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(Guid id) => Ok(await audit.GetEventsAsync(User.UserId(), id));

    [HttpGet("audit")]
    public async Task<ActionResult<ApplicationAuditResult>> GetAudit(Guid id) =>
        this.ToActionResult(await audit.GetApplicationJobAuditAsync(User.UserId(), id));
}
