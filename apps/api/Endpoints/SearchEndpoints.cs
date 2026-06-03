using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/searches", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.SavedSearches.Where(x => x.UserId == principal.UserId()).OrderByDescending(x => x.CreatedAt).ToListAsync());

        authed.MapPost("/searches", async (SavedSearch input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            input.Id = Guid.NewGuid();
            input.UserId = principal.UserId();
            input.Platform = "linkedin";
            db.SavedSearches.Add(input);
            await db.SaveChangesAsync();
            return Results.Created($"/api/searches/{input.Id}", input);
        });

        authed.MapPut("/searches/{id:guid}", async (Guid id, SavedSearch input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var search = await db.SavedSearches.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (search is null) return Results.NotFound();
            search.Apply(input);
            await db.SaveChangesAsync();
            return Results.Ok(search);
        });

        authed.MapDelete("/searches/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var search = await db.SavedSearches.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (search is null) return Results.NotFound();
            db.SavedSearches.Remove(search);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return authed;
    }
}
