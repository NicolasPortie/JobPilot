using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/resume/ocr")]
public sealed class ResumeOcrController(ResumeOcrService ocr) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(ocr.GetStatus());
}
