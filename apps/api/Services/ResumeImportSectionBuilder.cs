using System.Text.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class ResumeImportSectionBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<ResumeSection> BuildSections(Guid userId, Guid versionId, ResumeProfileImportData data, string rawText)
    {
        var parsed = ResumeStructureParser.Parse(rawText);
        var sections = new List<ResumeSection>();
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            data.FullName, data.Email, data.Phone, data.City, data.State, data.Country,
            data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl, headline = parsed.Headline
        });
        AddSection(sections, userId, versionId, "summary", "Resumo", 20, new { text = data.ProfessionalSummary });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { items = data.Skills });
        AddItems(sections, userId, versionId, "experience", "Experiencia", 40, parsed.Experience);
        AddItems(sections, userId, versionId, "projects", "Projetos", 50, parsed.Projects);
        AddItems(sections, userId, versionId, "education", "Educacao", 60, parsed.Education);
        AddItems(sections, userId, versionId, "certifications", "Certificacoes", 70, parsed.Certifications);
        AddItems(sections, userId, versionId, "languages", "Idiomas", 75, parsed.Languages);
        AddSection(sections, userId, versionId, "raw_import", "Texto importado", 90, new { text = rawText });
        return sections;
    }

    /// <summary>Texto ATS da versão base: seções limpas, sem o texto bruto repetido.</summary>
    public static string BuildPlainResume(ResumeProfileImportData data, string rawText)
    {
        var parsed = ResumeStructureParser.Parse(rawText);
        var location = string.IsNullOrWhiteSpace(data.State) ? data.City : $"{data.City}/{data.State}";
        var model = ResumeComposer.FromParsed(
            parsed,
            data.ProfessionalSummary,
            data.Skills,
            ResumeTextSanitizer.JoinNonEmpty(" | ", data.Email, data.Phone, location),
            ResumeTextSanitizer.JoinNonEmpty(" | ", data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl),
            ResumeTextSanitizer.FirstNonEmpty(data.FullName, parsed.FullName));
        var rendered = ResumeComposer.Render(model, "ats_standard");
        // Texto sem títulos reconhecíveis: mantém o conteúdo original em vez de perdê-lo.
        return parsed.HasStructure || string.IsNullOrWhiteSpace(rawText) ? rendered : rendered + Environment.NewLine + Environment.NewLine + rawText.Trim();
    }

    private static void AddItems(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, List<Dictionary<string, string>> items)
    {
        if (items.Count > 0) AddSection(sections, userId, versionId, type, title, order, new { items });
    }

    private static void AddSection(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, object content) =>
        sections.Add(new ResumeSection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = versionId,
            SectionType = type,
            Title = title,
            SortOrder = order,
            ContentJson = JsonSerializer.Serialize(content, JsonOptions)
        });
}
