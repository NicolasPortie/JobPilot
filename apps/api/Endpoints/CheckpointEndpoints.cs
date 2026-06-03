using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class CheckpointEndpoints
{
    public static RouteGroupBuilder MapCheckpointEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/checkpoints", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.Checkpoints
                .Where(x => x.UserId == principal.UserId() && x.ResolutionStatus == "open")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync());

        authed.MapPost("/checkpoints/{id:guid}/resolve", async (Guid id, ResolveCheckpointRequest request, CheckpointResolutionService service, ClaimsPrincipal principal) =>
        {
            var checkpoint = await service.ResolveAsync(principal.UserId(), id, request);
            return checkpoint is null ? Results.NotFound() : Results.Ok(checkpoint);
        });

        return authed;
    }
}
