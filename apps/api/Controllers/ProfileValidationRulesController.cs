using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile-validation-rules")]
public sealed class ProfileValidationRulesController(CandidateProfileService profiles) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProfileValidationRuleResponse>>> List() =>
        Ok(await profiles.ListRulesAsync(User.UserId()));

    [HttpPost]
    public async Task<ActionResult<ProfileValidationRuleResponse>> Create(ProfileValidationRuleRequest request)
    {
        var result = await profiles.CreateRuleAsync(User.UserId(), request);
        return this.ToActionResult(result, $"/api/profile-validation-rules/{result.Value?.Id}");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProfileValidationRuleResponse>> Update(Guid id, ProfileValidationRuleRequest request) =>
        this.ToActionResult(await profiles.UpdateRuleAsync(User.UserId(), id, request));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ProfileValidationRuleResponse>> Delete(Guid id) =>
        this.ToActionResult(await profiles.DeleteRuleAsync(User.UserId(), id));
}
