using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace JobPilot.Api.Infrastructure;

public sealed class ActiveAgentDeviceRequirement : IAuthorizationRequirement;

public sealed class ActiveAgentDeviceHandler(IRepository<AgentDevice> devices)
    : AuthorizationHandler<ActiveAgentDeviceRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAgentDeviceRequirement requirement)
    {
        if (!context.User.IsInRole("agent"))
            return;

        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = context.User.FindFirstValue("user_id");
        if (!Guid.TryParse(subject, out var deviceId) ||
            !Guid.TryParse(userId, out var ownerId))
            return;

        var device = await devices.FirstOrDefaultAsync(candidate =>
            candidate.Id == deviceId &&
            candidate.UserId == ownerId &&
            candidate.Status == "active");

        if (device is not null)
            context.Succeed(requirement);
    }
}
