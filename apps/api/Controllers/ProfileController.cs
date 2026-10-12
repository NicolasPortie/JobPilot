using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(
    CandidateProfileService profiles,
    ResumeFileService files,
    AccountDeletionService accountDeletion,
    RetentionService retention) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CandidateProfileResponse>> Get() =>
        Ok(await profiles.GetAsync(User.UserId()));

    [HttpPut]
    public async Task<ActionResult<CandidateProfileResponse>> Save(CandidateProfileRequest request) =>
        Ok(await profiles.SaveAsync(User.UserId(), request));

    [HttpGet("validate")]
    public async Task<ActionResult<ProfileValidationResult>> Validate() =>
        Ok(await profiles.ValidateAsync(User.UserId()));

    [HttpGet("completion")]
    public async Task<ActionResult<ProfileCompletionResponse>> Completion() =>
        Ok(await profiles.CompletionAsync(User.UserId()));

    [HttpGet("retention")]
    public ActionResult<RetentionPolicyResponse> Retention() =>
        Ok(retention.GetPolicy());

    [HttpDelete]
    public async Task<IActionResult> DeleteProfile(CancellationToken cancellationToken)
    {
        var removed = await accountDeletion.DeleteProfileAsync(User.UserId(), cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpPost("import-resume-file")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Analysis)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<ResumeFileImportResponse>> ImportResumeFile(
        IFormFile file,
        [FromQuery] bool? fetchPublicLinks,
        [FromQuery] bool? useAi) =>
        Ok(await files.ImportUploadAsync(User.UserId(), file, fetchPublicLinks ?? false, useAi ?? true));
}
