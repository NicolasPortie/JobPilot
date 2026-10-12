using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/ai/answer-draft")]
[EnableRateLimiting(RateLimitPolicies.Agent)]
public sealed class AiAnswerDraftController(AgentManagementService agent) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AnswerDraftResponse>> Draft(AnswerDraftRequest request) =>
        Ok(await agent.DraftAnswerAsync(User.UserId(), request));
}
