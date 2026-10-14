using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeQueryService(
    IRepository<ResumeImport> imports,
    IRepository<ResumeDocument> documents,
    IRepository<ResumeVersion> versions)
{
    public Task<List<ResumeImportSummary>> ListImportsAsync(Guid userId) =>
        imports.Query()
            .Where(import => import.UserId == userId)
            .OrderByDescending(import => import.CreatedAt)
            .Take(50)
            .Select(import => new ResumeImportSummary(
                import.Id,
                import.SourceFileName,
                import.SourceKind,
                import.ExtractionMethod,
                import.Classification,
                import.TextQualityScore,
                import.Confidence,
                import.RequiresOcr,
                import.RequiresReview,
                import.WordCount,
                import.CreatedAt))
            .ToListAsync();

    public async Task<List<ResumeDocumentSummary>> ListDocumentsAsync(Guid userId) =>
        (await documents.Query()
            .Where(document => document.UserId == userId)
            .Include(document => document.Versions)
            .OrderByDescending(document => document.UpdatedAt)
            .Take(50)
            .ToListAsync())
        .Select(document => new ResumeDocumentSummary(
            document.Id,
            document.Title,
            document.Status,
            document.SourceFileName,
            document.SourceKind,
            document.UpdatedAt,
            document.Versions
                .OrderByDescending(version => version.UpdatedAt)
                .Select(ToVersionSummary)
                .ToArray()))
        .ToList();

    public async Task<ResumeVersionDetail?> GetVersionAsync(Guid userId, Guid id)
    {
        var version = await versions.Query()
            .Include(candidate => candidate.Sections)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        return version is null
            ? null
            : new ResumeVersionDetail(
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
                    .ToArray());
    }

    /// <summary>Último currículo importado de arquivo; é a base de qualquer adaptação por vaga.</summary>
    public async Task<Guid?> LatestBaseVersionIdAsync(Guid userId)
    {
        var imported = await versions.Query()
            .Where(version => version.UserId == userId && version.Title == BaseVersionTitle)
            .OrderByDescending(version => version.CreatedAt)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync();
        if (imported is not null) return imported;

        return await versions.Query()
            .Where(version => version.UserId == userId && !version.AtsReportJson.Contains("tailored_resume"))
            .OrderByDescending(version => version.CreatedAt)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync();
    }

    public const string BaseVersionTitle = "Versão base importada";

    private static ResumeVersionSummary ToVersionSummary(ResumeVersion version) =>
        new(version.Id, version.Title, version.Status, version.TargetRole, version.TargetCompany, version.AtsScore, version.UpdatedAt, KindOf(version));

    private static string KindOf(ResumeVersion version) =>
        version.Title == BaseVersionTitle ? "base"
        : version.AtsReportJson.Contains("tailored_resume") ? "tailored"
        : version.AtsReportJson.Contains("generic_ats_resume") ? "ats"
        : "other";
}
