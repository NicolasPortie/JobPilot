using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/jobs", async (string? status, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var query = db.JobLeads.Where(x => x.UserId == userId).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
            var jobs = await query
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.FoundAt)
                .Take(200)
                .Select(x => new JobLeadDto(
                    x.Id,
                    x.Title,
                    x.Company,
                    x.Location,
                    x.Description,
                    x.Url,
                    x.Platform,
                    x.ApplyPlatform,
                    x.ApplySurface,
                    x.ApplyUrl,
                    x.AutomationSupport,
                    x.AutomationReason,
                    x.Score,
                    x.ScoreReason,
                    x.Status,
                    x.IsRemote,
                    x.FilteredReason,
                    x.FoundAt,
                    db.ApplicationJobs
                        .Where(a => a.UserId == userId && a.JobLeadId == x.Id && a.State != JobStatuses.Applied)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => new JobLeadResumeDto(a.ResumeVersionId, a.ResumeExportId, a.ResumeFilePath))
                        .FirstOrDefault()))
                .ToListAsync();
            return Results.Ok(jobs);
        });

        authed.MapGet("/jobs/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var job = await db.JobLeads
                .Where(x => x.Id == id && x.UserId == userId)
                .Select(x => new JobLeadDto(
                    x.Id,
                    x.Title,
                    x.Company,
                    x.Location,
                    x.Description,
                    x.Url,
                    x.Platform,
                    x.ApplyPlatform,
                    x.ApplySurface,
                    x.ApplyUrl,
                    x.AutomationSupport,
                    x.AutomationReason,
                    x.Score,
                    x.ScoreReason,
                    x.Status,
                    x.IsRemote,
                    x.FilteredReason,
                    x.FoundAt,
                    db.ApplicationJobs
                        .Where(a => a.UserId == userId && a.JobLeadId == x.Id && a.State != JobStatuses.Applied)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => new JobLeadResumeDto(a.ResumeVersionId, a.ResumeExportId, a.ResumeFilePath))
                        .FirstOrDefault()))
                .FirstOrDefaultAsync();
            return job is null ? Results.NotFound() : Results.Ok(job);
        });

        authed.MapPatch("/jobs/{id:guid}/status", async (Guid id, StatusRequest body, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var job = await db.JobLeads.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (job is null) return Results.NotFound();
            job.Status = body.Status;
            await db.SaveChangesAsync();
            return Results.Ok(job);
        });

        authed.MapPatch("/jobs/{id:guid}/resume", async (Guid id, ApplicationJobResumeRequest request, ApplicationResumeService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.AssignToJobLeadAsync(principal.UserId(), id, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapGet("/jobs/{id:guid}/fields", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetFieldsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/answers", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetAnswersByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/steps", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetStepsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/trace-steps", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetTraceStepsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/checkpoints", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetCheckpointsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/events", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetEventsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/learnings", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetLearningsByLeadAsync(principal.UserId(), id));

        authed.MapGet("/jobs/{id:guid}/audit", async (Guid id, ApplicationAuditService audit, ClaimsPrincipal principal) =>
            await audit.GetLeadAuditAsync(principal.UserId(), id));

        authed.MapGet("/jobs/filtered", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.JobLeads.Where(x => x.UserId == principal.UserId() && x.Status == "ignored" && !string.IsNullOrWhiteSpace(x.FilteredReason))
                .OrderByDescending(x => x.FoundAt)
                .Take(100)
                .ToListAsync());

        return authed;
    }
}

public record StatusRequest(string Status);

public record JobLeadResumeDto(Guid? ResumeVersionId, Guid? ResumeExportId, string ResumeFilePath);

public record JobLeadDto(
    Guid Id,
    string Title,
    string Company,
    string Location,
    string Description,
    string Url,
    string Platform,
    string ApplyPlatform,
    string ApplySurface,
    string ApplyUrl,
    string AutomationSupport,
    string AutomationReason,
    int Score,
    string ScoreReason,
    string Status,
    bool IsRemote,
    string FilteredReason,
    DateTimeOffset FoundAt,
    JobLeadResumeDto? Resume);
