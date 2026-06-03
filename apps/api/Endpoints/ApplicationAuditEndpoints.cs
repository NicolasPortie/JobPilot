using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class ApplicationAuditEndpoints
{
    public static RouteGroupBuilder MapApplicationAuditEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/application-jobs/{id:guid}/fields", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetFieldsAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/answers", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetAnswersAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/steps", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetStepsAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/trace-steps", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetTraceStepsAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/checkpoints", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetCheckpointsAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/events", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            Results.Ok(await audit.GetEventsAsync(principal.UserId(), id)));

        authed.MapGet("/application-jobs/{id:guid}/audit", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetApplicationJobAuditAsync(principal.UserId(), id));

        return authed;
    }
}
