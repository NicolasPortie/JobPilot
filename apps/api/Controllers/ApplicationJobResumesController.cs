using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/application-jobs/{id:guid}/resume")]
public sealed class ApplicationJobResumesController(ApplicationResumeService resumes) : ControllerBase
{
    [HttpPatch]
    public async Task<ActionResult<ApplicationJobResumeResponse>> Assign(Guid id, ApplicationJobResumeRequest request) =>
        Ok(await resumes.AssignAsync(User.UserId(), id, request));
}
