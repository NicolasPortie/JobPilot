using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "ActiveAgent")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/agent/platform-credentials")]
public sealed class AgentPlatformCredentialsController(IPlatformCredentialService credentials) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AgentPlatformCredentialResponse>>> List() =>
        Ok(await credentials.ListForAgentAsync(User.AgentUserId()));
}
