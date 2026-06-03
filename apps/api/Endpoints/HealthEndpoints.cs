using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/health/operational", async (OperationalHealthService health, ClaimsPrincipal principal) =>
            Results.Ok(await health.GetAsync(principal.UserId())));

        authed.MapPost("/health/operational/recover", async (ApplicationMaintenanceService maintenance, ClaimsPrincipal principal) =>
            Results.Ok(await maintenance.RecoverStaleWorkAsync(DateTimeOffset.UtcNow, principal.UserId())));

        return authed;
    }
}
