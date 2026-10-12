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
[Route("api/agent")]
[EnableRateLimiting(RateLimitPolicies.Agent)]
public sealed class AgentManagementController(AgentManagementService agent) : ControllerBase
{
    [HttpGet("policy")]
    public async Task<ActionResult<AgentPolicyResponse>> GetPolicy() =>
        Ok(await agent.GetPolicyAsync(User.UserId()));

    [HttpPut("policy")]
    public async Task<ActionResult<AgentPolicyResponse>> SavePolicy(AgentPolicyRequest request) =>
        Ok(await agent.SavePolicyAsync(User.UserId(), request));

    [HttpGet("history")]
    public async Task<ActionResult<AgentHistoryResponse>> GetHistory() =>
        Ok(await agent.GetHistoryAsync(User.UserId()));

    [HttpPost("pair")]
    public async Task<ActionResult<PairDeviceResponse>> PairDevice(PairDeviceRequest request) =>
        Ok(await agent.PairDeviceAsync(User.UserId(), request));
}
