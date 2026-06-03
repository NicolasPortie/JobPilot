using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class AutomationEndpoints
{
    public static RouteGroupBuilder MapAutomationEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapPost("/automation/discover", async (AutomationDiscoverRequest request, AutomationProcessService automation, ClaimsPrincipal principal) =>
            await automation.StartDiscoverAsync(principal.UserId(), request));

        authed.MapPost("/automation/apply", async (AutomationApplyRequest request, AutomationProcessService automation, ClaimsPrincipal principal) =>
            await automation.StartApplyAsync(principal.UserId(), request));

        return authed;
    }
}
