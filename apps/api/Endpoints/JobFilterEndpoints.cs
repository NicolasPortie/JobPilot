using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class JobFilterEndpoints
{
    public static RouteGroupBuilder MapJobFilterEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/job-filters", async (JobFilterService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.GetOrCreateAsync(principal.UserId())));

        authed.MapPut("/job-filters", async (JobFilterRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var filter = await db.JobFilters.FirstOrDefaultAsync(x => x.UserId == userId);
            if (filter is null)
            {
                filter = new JobFilter { Id = Guid.NewGuid(), UserId = userId };
                db.JobFilters.Add(filter);
            }
            filter.Apply(new JobFilter
            {
                AllowedSeniorities = request.AllowedSeniorities,
                BlockedSeniorities = request.BlockedSeniorities,
                AllowedRoleKeywords = request.AllowedRoleKeywords,
                BlockedRoleKeywords = request.BlockedRoleKeywords,
                AllowedWorkplaceTypes = request.AllowedWorkplaceTypes,
                BlockedWorkplaceTypes = request.BlockedWorkplaceTypes,
                AllowedApplyTypes = request.AllowedApplyTypes,
                MinScore = request.MinScore,
                AutoIgnoreLowScore = request.AutoIgnoreLowScore
            });
            await db.SaveChangesAsync();
            return Results.Ok(filter);
        });

        return authed;
    }
}
