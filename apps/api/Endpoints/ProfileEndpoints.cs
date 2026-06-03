using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/profile", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            return Results.Ok(profile ?? new CandidateProfile { UserId = userId });
        });

        authed.MapPut("/profile", async (CandidateProfile input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (profile is null)
            {
                profile = input;
                profile.Id = Guid.NewGuid();
                profile.UserId = userId;
                db.CandidateProfiles.Add(profile);
            }
            else
            {
                profile.Apply(input);
            }

            await db.SaveChangesAsync();
            return Results.Ok(profile);
        });

        authed.MapGet("/profile/validate", async (ProfileGuard guard, ClaimsPrincipal principal) =>
        {
            var issues = await guard.ValidateAsync(principal.UserId());
            return Results.Ok(new { valid = issues.All(i => !i.IsCritical), critical = issues.Any(i => i.IsCritical), issues });
        });

        authed.MapPost("/profile/import-resume", async (ProfileImportRequest request, ProfileImportService importer, ClaimsPrincipal principal) =>
            Results.Ok(await importer.ImportAsync(principal.UserId(), request)));

        authed.MapPost("/profile/import-resume-file", async (IFormFile file, bool? fetchPublicLinks, bool? useAi, ResumeFileService files, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await files.ImportUploadAsync(principal.UserId(), file, fetchPublicLinks ?? true, useAi ?? true));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).DisableAntiforgery();

        authed.MapGet("/resume/ocr/status", (ResumeOcrService ocr) =>
            Results.Ok(ocr.GetStatus()));

        authed.MapGet("/profile-validation-rules", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
            await db.ProfileValidationRules.Where(x => x.UserId == principal.UserId()).OrderBy(x => x.FieldName).ToListAsync());

        authed.MapPost("/profile-validation-rules", async (ProfileValidationRuleRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var rule = new ProfileValidationRule
            {
                Id = Guid.NewGuid(),
                UserId = principal.UserId(),
                FieldName = request.FieldName,
                ValidationType = request.ValidationType,
                ValidationValue = request.ValidationValue,
                ErrorMessage = request.ErrorMessage,
                IsCritical = request.IsCritical
            };
            db.ProfileValidationRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/profile-validation-rules/{rule.Id}", rule);
        });

        authed.MapPut("/profile-validation-rules/{id:guid}", async (Guid id, ProfileValidationRuleRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var rule = await db.ProfileValidationRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (rule is null) return Results.NotFound();
            rule.FieldName = request.FieldName;
            rule.ValidationType = request.ValidationType;
            rule.ValidationValue = request.ValidationValue;
            rule.ErrorMessage = request.ErrorMessage;
            rule.IsCritical = request.IsCritical;
            await db.SaveChangesAsync();
            return Results.Ok(rule);
        });

        authed.MapDelete("/profile-validation-rules/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var rule = await db.ProfileValidationRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.UserId());
            if (rule is null) return Results.NotFound();
            db.ProfileValidationRules.Remove(rule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return authed;
    }
}
