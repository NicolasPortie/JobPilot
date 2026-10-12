using System.Text.Json;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeSectionService(
    IRepository<ResumeSection> sections,
    IRepository<ResumeVersion> versions,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeSectionDto[]> ListAsync(Guid userId, Guid versionId)
    {
        if (!await versions.Query().AnyAsync(x => x.Id == versionId && x.UserId == userId))
            throw new KeyNotFoundException("Versao de curriculo nao encontrada.");

        return await sections.Query()
            .Where(x => x.UserId == userId && x.ResumeVersionId == versionId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeSectionDto(x.Id, x.SectionType, x.Title, x.SortOrder, x.ContentJson))
            .ToArrayAsync();
    }

    public async Task<ResumeSectionDto> AddAsync(Guid userId, Guid versionId, CreateResumeSectionRequest request)
    {
        var version = await versions.FirstOrDefaultAsync(x => x.Id == versionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");

        var currentSections = await LoadSectionsAsync(version);
        var maxOrder = currentSections.Select(x => (int?)x.SortOrder).Max() ?? 0;

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

        sections.Add(section);
        currentSections.Add(section);
        RebuildPlainText(version, currentSections);
        await unitOfWork.SaveChangesAsync();

        return new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson);
    }

    public async Task<ResumeSectionDto> UpdateAsync(Guid userId, Guid sectionId, UpdateResumeSectionRequest request)
    {
        var section = await sections.Query()
            .Include(x => x.Version)
            .FirstOrDefaultAsync(x => x.Id == sectionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Seção não encontrada.");

        if (!string.IsNullOrWhiteSpace(request.Title))
            section.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.ContentJson))
            section.ContentJson = request.ContentJson;
        if (request.SortOrder is { } order)
            section.SortOrder = order;

        if (section.Version is not null)
            RebuildPlainText(section.Version, await LoadSectionsAsync(section.Version));

        await unitOfWork.SaveChangesAsync();

        return new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson);
    }

    public async Task<ResumeSectionDto[]> ReorderAsync(Guid userId, Guid versionId, ReorderResumeSectionsRequest request)
    {
        var version = await versions.FirstOrDefaultAsync(x => x.Id == versionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");

        var sectionIds = request.SectionIds
            .Where(id => id != Guid.Empty)
            .ToArray();
        if (sectionIds.Length == 0)
            throw new InvalidOperationException("Informe a ordem das seções.");
        if (sectionIds.Distinct().Count() != sectionIds.Length)
            throw new InvalidOperationException("A lista de seções contém itens duplicados.");

        var existingSections = await sections.Query()
            .Where(x => x.UserId == userId && x.ResumeVersionId == versionId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        if (existingSections.Count == 0)
            throw new InvalidOperationException("Não há seções para reordenar.");
        if (existingSections.Count != sectionIds.Length)
            throw new InvalidOperationException("A nova ordem deve conter todas as seções da versão.");

        var sectionMap = existingSections.ToDictionary(x => x.Id);
        foreach (var id in sectionIds)
        {
            if (!sectionMap.ContainsKey(id))
                throw new InvalidOperationException("A nova ordem contém seção inválida para esta versão.");
        }

        for (var index = 0; index < sectionIds.Length; index++)
        {
            sectionMap[sectionIds[index]].SortOrder = (index + 1) * 10;
        }

        RebuildPlainText(version, existingSections);
        await unitOfWork.SaveChangesAsync();

        return existingSections
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeSectionDto(x.Id, x.SectionType, x.Title, x.SortOrder, x.ContentJson))
            .ToArray();
    }

    public async Task DeleteAsync(Guid userId, Guid sectionId)
    {
        var section = await sections.Query()
            .Include(x => x.Version)
            .FirstOrDefaultAsync(x => x.Id == sectionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Seção não encontrada.");

        var version = section.Version;
        sections.Remove(section);

        if (version is not null)
        {
            var remainingSections = (await LoadSectionsAsync(version))
                .Where(candidate => candidate.Id != section.Id)
                .ToList();
            RebuildPlainText(version, remainingSections);
        }

        await unitOfWork.SaveChangesAsync();
    }

    private Task<List<ResumeSection>> LoadSectionsAsync(ResumeVersion version) =>
        sections.Query()
            .Where(x => x.UserId == version.UserId && x.ResumeVersionId == version.Id)
            .ToListAsync();

    private static void RebuildPlainText(ResumeVersion version, IReadOnlyCollection<ResumeSection> currentSections)
    {
        var parts = new List<string>();
        foreach (var section in currentSections.OrderBy(x => x.SortOrder))
        {
            var text = ExtractTextFromJson(section.ContentJson);
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(section.Title.ToUpperInvariant());
                parts.Add(text);
                parts.Add("");
            }
        }

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
