using System.Text.Json;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeSectionService(JobPilotDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeSectionDto[]> ListAsync(Guid userId, Guid versionId)
    {
        if (!await db.ResumeVersions.AnyAsync(x => x.Id == versionId && x.UserId == userId))
            throw new InvalidOperationException("Versao de curriculo nao encontrada.");

        var sections = await db.ResumeSections
            .Where(x => x.UserId == userId && x.ResumeVersionId == versionId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeSectionDto(x.Id, x.SectionType, x.Title, x.SortOrder, x.ContentJson))
            .ToArrayAsync();
        return sections;
    }

    public async Task<ResumeSectionDto> AddAsync(Guid userId, Guid versionId, CreateResumeSectionRequest request)
    {
        var version = await db.ResumeVersions.FirstOrDefaultAsync(x => x.Id == versionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao de curriculo nao encontrada.");

        var maxOrder = await db.ResumeSections
            .Where(x => x.ResumeVersionId == versionId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? 0;

        var section = new ResumeSection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = versionId,
            SectionType = NormalizeType(request.SectionType),
            Title = request.Title,
            SortOrder = maxOrder + 10,
            ContentJson = request.ContentJson
        };

        db.ResumeSections.Add(section);
        await db.SaveChangesAsync();
        await RebuildPlainTextAsync(version);
        await db.SaveChangesAsync();

        return new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson);
    }

    public async Task<ResumeSectionDto> UpdateAsync(Guid userId, Guid sectionId, UpdateResumeSectionRequest request)
    {
        var section = await db.ResumeSections
            .Include(x => x.Version)
            .FirstOrDefaultAsync(x => x.Id == sectionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Secao nao encontrada.");

        if (!string.IsNullOrWhiteSpace(request.Title))
            section.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.ContentJson))
            section.ContentJson = request.ContentJson;
        if (request.SortOrder is { } order)
            section.SortOrder = order;

        if (section.Version is not null)
            await RebuildPlainTextAsync(section.Version);

        await db.SaveChangesAsync();

        return new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson);
    }

    public async Task<ResumeSectionDto[]> ReorderAsync(Guid userId, Guid versionId, ReorderResumeSectionsRequest request)
    {
        var version = await db.ResumeVersions
            .FirstOrDefaultAsync(x => x.Id == versionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao de curriculo nao encontrada.");

        var sectionIds = request.SectionIds
            .Where(id => id != Guid.Empty)
            .ToArray();
        if (sectionIds.Length == 0)
            throw new InvalidOperationException("Informe a ordem das secoes.");
        if (sectionIds.Distinct().Count() != sectionIds.Length)
            throw new InvalidOperationException("A lista de secoes contem itens duplicados.");

        var sections = await db.ResumeSections
            .Where(x => x.UserId == userId && x.ResumeVersionId == versionId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        if (sections.Count == 0)
            throw new InvalidOperationException("Nao ha secoes para reordenar.");
        if (sections.Count != sectionIds.Length)
            throw new InvalidOperationException("A nova ordem deve conter todas as secoes da versao.");

        var sectionMap = sections.ToDictionary(x => x.Id);
        foreach (var sectionId in sectionIds)
        {
            if (!sectionMap.ContainsKey(sectionId))
                throw new InvalidOperationException("A nova ordem contem secao invalida para esta versao.");
        }

        for (var index = 0; index < sectionIds.Length; index++)
        {
            sectionMap[sectionIds[index]].SortOrder = (index + 1) * 10;
        }

        await db.SaveChangesAsync();
        await RebuildPlainTextAsync(version);
        await db.SaveChangesAsync();

        return sections
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeSectionDto(x.Id, x.SectionType, x.Title, x.SortOrder, x.ContentJson))
            .ToArray();
    }

    public async Task DeleteAsync(Guid userId, Guid sectionId)
    {
        var section = await db.ResumeSections
            .Include(x => x.Version)
            .FirstOrDefaultAsync(x => x.Id == sectionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Secao nao encontrada.");

        var version = section.Version;
        db.ResumeSections.Remove(section);
        await db.SaveChangesAsync();

        if (version is not null)
            await RebuildPlainTextAsync(version);

        await db.SaveChangesAsync();
    }

    private async Task RebuildPlainTextAsync(ResumeVersion version)
    {
        var sections = await db.ResumeSections
            .Where(x => x.UserId == version.UserId && x.ResumeVersionId == version.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var parts = new List<string>();
        foreach (var section in sections)
        {
            var text = ExtractTextFromJson(section.ContentJson);
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(section.Title.ToUpperInvariant());
                parts.Add(text);
                parts.Add("");
            }
        }

        if (!string.IsNullOrWhiteSpace(version.PlainText) && !parts.Any())
            return; // keep existing if no sections

        version.PlainText = string.Join("\n", parts).Trim();
        version.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string ExtractTextFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var document = JsonDocument.Parse(json);
            var parts = new List<string>();
            CollectJsonText(document.RootElement, parts);
            return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        }
        catch
        {
            return json;
        }
    }

    private static void CollectJsonText(JsonElement element, List<string> parts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                parts.Add(element.GetString() ?? "");
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectJsonText(item, parts);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) CollectJsonText(property.Value, parts);
                break;
        }
    }

    private static string NormalizeType(string value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace(" ", "_");
        return normalized switch
        {
            "experience" or "experiencia" or "work_experience" => "experience",
            "projects" or "projetos" => "projects",
            "education" or "educacao" or "formacao" => "education",
            "certifications" or "certificacoes" or "certificacao" => "certifications",
            "languages" or "idiomas" or "language" => "languages",
            "skills" or "competencias" or "habilidades" => "skills",
            "summary" or "resumo" => "summary",
            "identity" or "contact" or "contato" => "identity",
            _ => normalized
        };
    }
}

public record CreateResumeSectionRequest(string SectionType, string Title, string ContentJson);
public record UpdateResumeSectionRequest(string? Title = null, string? ContentJson = null, int? SortOrder = null);
public record ReorderResumeSectionsRequest(Guid[] SectionIds);
