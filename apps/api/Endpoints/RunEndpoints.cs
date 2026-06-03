using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class RunEndpoints
{
    public static RouteGroupBuilder MapRunEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapPost("/runs", async (CreateRunRequest request, ApplicationRunService runs, ClaimsPrincipal principal) =>
            await runs.CreateAsync(principal.UserId(), request));

        authed.MapGet("/runs", async (ApplicationRunService runs, ClaimsPrincipal principal) =>
            await runs.ListAsync(principal.UserId()));

        authed.MapPatch("/application-jobs/{id:guid}/resume", async (Guid id, ApplicationJobResumeRequest request, ApplicationResumeService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.AssignAsync(principal.UserId(), id, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return authed;
    }
}
