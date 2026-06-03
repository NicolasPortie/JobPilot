using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class ResumeEndpoints
{
    public static RouteGroupBuilder MapResumeEndpoints(this RouteGroupBuilder authed)
    {
        authed.MapGet("/resume/imports", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var imports = await db.ResumeImports
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .Select(x => new ResumeImportSummary(
                    x.Id,
                    x.SourceFileName,
                    x.SourceKind,
                    x.ExtractionMethod,
                    x.Classification,
                    x.TextQualityScore,
                    x.Confidence,
                    x.RequiresOcr,
                    x.RequiresReview,
                    x.WordCount,
                    x.CreatedAt))
                .ToListAsync();

            return Results.Ok(imports);
        });

        authed.MapGet("/resume/documents", async (JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var documents = await db.ResumeDocuments
                .Where(x => x.UserId == userId)
                .Include(x => x.Versions)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(50)
                .ToListAsync();

            return Results.Ok(documents.Select(document => new ResumeDocumentSummary(
                document.Id,
                document.Title,
                document.Status,
                document.SourceFileName,
                document.SourceKind,
                document.UpdatedAt,
                document.Versions
                    .OrderByDescending(version => version.UpdatedAt)
                    .Select(ToVersionSummary)
                    .ToArray())));
        });

        authed.MapGet("/resume/layouts", () => Results.Ok(ResumeTailoringService.LayoutOptions));

        authed.MapGet("/resume/versions/{id:guid}", async (Guid id, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.UserId();
            var version = await db.ResumeVersions
                .Include(x => x.Sections)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (version is null) return Results.NotFound();

            return Results.Ok(new ResumeVersionDetail(
                version.Id,
                version.ResumeDocumentId,
                version.Title,
                version.Status,
                version.TargetRole,
                version.TargetCompany,
                version.AtsScore,
                version.PlainText,
                version.Sections
                    .OrderBy(section => section.SortOrder)
                    .Select(section => new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson))
                    .ToArray()));
        });

        // Resume sections editor
        authed.MapGet("/resume/versions/{id:guid}/sections", async (Guid id, ResumeSectionService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.ListAsync(principal.UserId(), id));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/resume/versions/{id:guid}/sections", async (Guid id, CreateResumeSectionRequest request, ResumeSectionService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.AddAsync(principal.UserId(), id, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/resume/versions/{id:guid}/sections/reorder", async (Guid id, ReorderResumeSectionsRequest request, ResumeSectionService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.ReorderAsync(principal.UserId(), id, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPut("/resume/sections/{sectionId:guid}", async (Guid sectionId, UpdateResumeSectionRequest request, ResumeSectionService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.UpdateAsync(principal.UserId(), sectionId, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapDelete("/resume/sections/{sectionId:guid}", async (Guid sectionId, ResumeSectionService service, ClaimsPrincipal principal) =>
        {
            try
            {
                await service.DeleteAsync(principal.UserId(), sectionId);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/resume/tailor", async (TailorResumeRequest request, ResumeTailoringService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.TailorAsync(principal.UserId(), request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/resume/ats-build", async (BuildGenericAtsResumeRequest request, ResumeTailoringService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.BuildGenericAtsAsync(principal.UserId(), request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapPost("/resume/versions/{id:guid}/exports", async (Guid id, CreateResumeExportRequest request, ResumeExportService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.ExportAsync(principal.UserId(), id, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapGet("/resume/exports/{id:guid}/download", async (Guid id, ResumeExportService service, ClaimsPrincipal principal) =>
        {
            var export = await service.GetAsync(principal.UserId(), id);
            if (export is null) return Results.NotFound();
            if (!File.Exists(export.FilePath)) return Results.NotFound(new { message = "Arquivo exportado nao encontrado." });

            var fileName = Path.GetFileName(export.FilePath);
            var contentType = export.Format.Equals("docx", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : export.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                : "text/plain; charset=utf-8";
            return Results.File(export.FilePath, contentType, fileName);
        });

        // Review answers for unknown requirements
        authed.MapPost("/resume/review-answers", async (SaveReviewAnswerRequest request, ResumeReviewAnswerService service, ClaimsPrincipal principal) =>
        {
            try
            {
                var answer = await service.SaveAsync(principal.UserId(), request);
                return Results.Ok(new { answer.Id, answer.JobRequirementId, answer.RequirementLabel, answer.AnswerValue, answer.AnswerType });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        authed.MapGet("/resume/review-answers", async (Guid? evidenceMapRunId, ResumeReviewAnswerService service, ClaimsPrincipal principal) =>
        {
            var answers = await service.ListAsync(principal.UserId(), evidenceMapRunId);
            return Results.Ok(answers.Select(a => new { a.Id, a.JobRequirementId, a.RequirementLabel, a.QuestionText, a.AnswerValue, a.AnswerType, a.EvidenceText, a.CreatedAt }));
        });

        authed.MapPost("/resume/review-answers/regenerate", async (RegenerateAfterAnswersRequest request, ResumeReviewAnswerService service, ClaimsPrincipal principal) =>
        {
            try
            {
                return Results.Ok(await service.RegenerateAsync(principal.UserId(), request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return authed;
    }

    private static ResumeVersionSummary ToVersionSummary(ResumeVersion version) =>
        new(version.Id, version.Title, version.Status, version.TargetRole, version.TargetCompany, version.AtsScore, version.UpdatedAt);
}
