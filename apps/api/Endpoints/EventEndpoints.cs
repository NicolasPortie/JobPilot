using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/events", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.ExecutionEvents
                .Where(x => x.UserId == principal.UserId())
                .OrderByDescending(x => x.OccurredAt)
                .Take(100)
                .ToListAsync());

        return authed;
    }
}
