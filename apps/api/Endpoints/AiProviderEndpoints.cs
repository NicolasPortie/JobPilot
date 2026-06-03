using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class AiProviderEndpoints
{
    public static RouteGroupBuilder MapAiProviderEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/ai/providers", async (AiProviderService providers, ClaimsPrincipal principal) =>
            await providers.ListAsync(principal.UserId()));

        authed.MapPost("/ai/providers", async (AiProviderRequest request, AiProviderService providers, ClaimsPrincipal principal) =>
            await providers.CreateAsync(principal.UserId(), request));

        authed.MapPut("/ai/providers/{id:guid}", async (Guid id, AiProviderRequest request, AiProviderService providers, ClaimsPrincipal principal) =>
            await providers.UpdateAsync(principal.UserId(), id, request));

        authed.MapDelete("/ai/providers/{id:guid}", async (Guid id, AiProviderService providers, ClaimsPrincipal principal) =>
            await providers.DeleteAsync(principal.UserId(), id));

        authed.MapPost("/ai/providers/{id:guid}/test", async (Guid id, AiProviderService providers, ClaimsPrincipal principal) =>
            await providers.TestAsync(principal.UserId(), id));

        return authed;
    }
}
