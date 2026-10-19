using System.Text.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

/// <summary>Transforma o mapa de evidências e a nota em textos e listas prontos para a tela.</summary>
public static class ResumeAnalysisSummaryBuilder
{
    private const int MaxSuggestions = 4;
    private const int MaxKeywordsInInsight = 3;
    private const int MaxProjectsInInsight = 2;

    public static ResumeAnalysisSummary ForTarget(
        int score,
        ResumeEvidenceMapDetail evidenceMap,
        IEnumerable<string> profileSkills,
        IEnumerable<ResumeSection> baseSections)
    {
        var requirements = evidenceMap.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new RequirementReviewItem(
                item.JobRequirementId,
                item.RequirementLabel,
                item.RequirementCategory,
                item.RequirementImportance,
                item.MatchStatus switch { "proven" => "proven", "adjacent" => "adjacent", _ => "missing" },
                item.EvidenceText))
            .ToArray();

        var proven = requirements.Where(item => item.Status == "proven").ToArray();
        var known = new HashSet<string>(profileSkills.Select(Normalize), StringComparer.Ordinal);
        var suggestions = requirements
            .Where(item => item.Status == "missing" && item.Category is not ("education" or "language" or "certification" or "domain"))
            .Select(item => item.Label.Trim())
            .Where(label => label.Length > 0 && !known.Contains(Normalize(label)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToArray();
        var focus = requirements.Where(item => item.Status == "missing")
            .Concat(requirements.Where(item => item.Status == "adjacent"))
            .Select(item => item.Label.Trim())
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxKeywordsInInsight)
            .ToArray();
        var projects = ProjectNames(baseSections).Take(MaxProjectsInInsight).ToArray();

        return new ResumeAnalysisSummary(
            Adherence(score, proven.Length, requirements.Length),
            requirements,
            proven.Select(item => item.Label).Where(label => !string.IsNullOrWhiteSpace(label)).ToArray(),
            suggestions,
            [
                new ResumeInsight("results", "Resultados concretos", requirements.Length > 0
                    ? $"{proven.Length} de {requirements.Length} requisitos comprovados. Vagas nesta área valorizam métricas e impacto: inclua números e resultados nas suas experiências."
                    : "Vagas nesta área valorizam métricas e impacto: inclua números e resultados nas suas experiências."),
                new ResumeInsight("keywords", "Palavras-chave importantes", focus.Length > 0
                    ? $"Use os termos mais relevantes da vaga no seu currículo, como {JoinList(focus)}, desde que você possa sustentá-los."
                    : "Use os termos mais relevantes da vaga no seu currículo para passar na triagem."),
                new ResumeInsight("projects", "Projetos relevantes", projects.Length > 0
                    ? $"Destaque projetos como {JoinList(projects)} para demonstrar experiência prática."
                    : "Destaque projetos que demonstrem sua experiência prática com as tecnologias da vaga.")
            ]);
    }

    /// <summary>Versão sem vaga: só há nota de estrutura ATS, sem requisitos para comparar.</summary>
    public static ResumeAnalysisSummary ForGeneric(int score) =>
        new(
            new AdherenceSummary(
                LevelFor(score),
                score >= 75 ? "Estrutura sólida" : score >= 55 ? "Estrutura razoável" : "Estrutura a melhorar",
                "Base ATS pronta",
                "Sem uma vaga para comparar, a nota mostra apenas a estrutura do currículo. Cole uma vaga para medir a aderência."),
            [], [], [], []);

    private static AdherenceSummary Adherence(int score, int proven, int total)
    {
        var level = LevelFor(score);
        var coverage = total > 0 ? $"{proven} de {total} requisitos comprovados no seu currículo." : "Sem requisitos extraídos desta vaga.";
        return level switch
        {
            "high" => new AdherenceSummary(level, "Alta aderência", "Ótima aderência!", $"Seu perfil atende a maioria dos requisitos. {coverage}"),
            "good" => new AdherenceSummary(level, "Boa aderência", "Boa base!", $"Seu perfil atende parte dos requisitos; revise as lacunas. {coverage}"),
            _ => new AdherenceSummary(level, "Baixa aderência", "Atenção aos requisitos", $"Há requisitos importantes sem evidência no seu currículo. {coverage}")
        };
    }

    private static string LevelFor(int score) => score >= 75 ? "high" : score >= 55 ? "good" : "low";

    private static IEnumerable<string> ProjectNames(IEnumerable<ResumeSection> sections)
    {
        var section = sections.FirstOrDefault(item => item.SectionType == "projects");
        if (section is null || string.IsNullOrWhiteSpace(section.ContentJson)) return [];

        try
        {
            using var document = JsonDocument.Parse(section.ContentJson);
            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return [];
            return items.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => Text(item, "name") is { Length: > 0 } name ? name : Text(item, "role"))
                .Where(name => name.Length > 0)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Text(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";

    private static string Normalize(string value) => JobTargetTextRules.NormalizeKeyword(value);

    private static string JoinList(string[] values) =>
        values.Length <= 2 ? string.Join(" e ", values) : $"{string.Join(", ", values[..^1])} e {values[^1]}";
}
