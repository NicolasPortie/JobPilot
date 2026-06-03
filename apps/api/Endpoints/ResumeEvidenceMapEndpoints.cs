using System.Security.Claims;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Endpoints;

public static class ResumeEvidenceMapEndpoints
{
    public static RouteGroupBuilder MapResumeEvidenceMapEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapPost("/resume/evidence-maps", async (BuildResumeEvidenceMapRequest request, ResumeEvidenceMapService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.BuildAsync(principal.UserId(), request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapGet("/resume/evidence-maps/{id:guid}", async (Guid id, ResumeEvidenceMapService service, ClaimsPrincipal principal) =>
        {
            var map = await service.GetAsync(principal.UserId(), id);
            return map is null ? Results.NotFound() : Results.Ok(map);
        });

        return authed;
    }
}
