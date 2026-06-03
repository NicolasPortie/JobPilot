using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class PlatformCredentialEndpoints
{
    public static RouteGroupBuilder MapPlatformCredentialEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/platform-credentials", async (PlatformCredentialService credentials, ClaimsPrincipal principal) =>
            Results.Ok(await credentials.ListAsync(principal.UserId())));

        authed.MapPost("/platform-credentials", async (PlatformCredentialRequest request, PlatformCredentialService credentials, ClaimsPrincipal principal) =>
            await credentials.CreateAsync(principal.UserId(), request));

        authed.MapPut("/platform-credentials/{id:guid}", async (Guid id, PlatformCredentialRequest request, PlatformCredentialService credentials, ClaimsPrincipal principal) =>
            await credentials.UpdateAsync(principal.UserId(), id, request));

        authed.MapDelete("/platform-credentials/{id:guid}", async (Guid id, PlatformCredentialService credentials, ClaimsPrincipal principal) =>
            await credentials.DeleteAsync(principal.UserId(), id));

        authed.MapGet("/agent/platform-credentials", async (PlatformCredentialService credentials, ClaimsPrincipal principal) =>
            Results.Ok(await credentials.ListForAgentAsync(principal.AgentUserId())));

        return authed;
    }
}
