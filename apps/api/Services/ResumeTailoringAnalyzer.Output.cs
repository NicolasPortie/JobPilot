using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static partial class ResumeTailoringAnalyzer
{
    public static List<ResumeSection> BuildSections(Guid userId, Guid versionId, CandidateProfile? profile, JobTarget target, TailoringAnalysis analysis, string plainText)
    {
        var sections = new List<ResumeSection>();
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            fullName = profile?.FullName ?? "",
            email = profile?.Email ?? "",
            phone = profile?.Phone ?? "",
            city = profile?.City ?? "",
            state = profile?.State ?? "",
            country = profile?.Country ?? "",
            linkedinUrl = profile?.LinkedinUrl ?? "",
            githubUrl = profile?.GithubUrl ?? "",
            portfolioUrl = profile?.PortfolioUrl ?? ""
        });
        AddSection(sections, userId, versionId, "target", "Vaga alvo", 20, new { target.Id, target.Title, target.Company, target.Location, target.Seniority, target.Workplace });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { proven = analysis.IncludedKeywords, adjacent = analysis.AdjacentKeywords, excluded = analysis.ExcludedKeywords });
        AddSection(sections, userId, versionId, "checkpoints", "Pendencias", 80, new { items = analysis.Checkpoints });
        AddSection(sections, userId, versionId, "plain_text", "Texto ATS", 90, new { text = plainText });
        return sections;
    }

    public static ResumeSectionDiff[] BuildSectionDiffs(ResumeVersion baseVersion, ResumeVersion tailoredVersion)
    {
        var before = baseVersion.Sections
            .GroupBy(section => section.SectionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).First(), StringComparer.OrdinalIgnoreCase);
        var after = tailoredVersion.Sections
            .GroupBy(section => section.SectionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).First(), StringComparer.OrdinalIgnoreCase);

        var keys = before.Keys
            .Concat(after.Keys)
            .Append("plain_text")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SectionDiffOrder)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keys.Select(key =>
            {
                before.TryGetValue(key, out var beforeSection);
                after.TryGetValue(key, out var afterSection);
                var beforeText = key.Equals("plain_text", StringComparison.OrdinalIgnoreCase)
                    ? baseVersion.PlainText
                    : SectionPreview(beforeSection);
                var afterText = key.Equals("plain_text", StringComparison.OrdinalIgnoreCase)
                    ? tailoredVersion.PlainText
                    : SectionPreview(afterSection);
                var status = SectionDiffStatus(beforeText, afterText);
                var title = ResumeTextSanitizer.FirstNonEmpty(afterSection?.Title, beforeSection?.Title, SectionDiffTitle(key));
                return new ResumeSectionDiff(key, title, status, Preview(beforeText), Preview(afterText), afterSection?.Id);
            })
            .Where(diff => diff.Status != "unchanged" || diff.SectionType == "plain_text")
            .ToArray();
    }

    public static ResumeReviewQuestion[] BuildReviewQuestions(ResumeEvidenceMapDetail map) =>
        map.Items
            .Where(x => x.MatchStatus == "unknown")
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeReviewQuestion(
                x.JobRequirementId,
                x.RequirementLabel,
                x.RequirementCategory,
                x.RequirementImportance,
                ReviewQuestionText(x),
                x.Reason))
            .ToArray();

    public static string ReviewQuestionText(ResumeRequirementEvidenceDto item)
    {
        var label = item.RequirementLabel.Trim();
        return item.RequirementCategory switch
        {
            "language" => $"Voce tem proficiencia real em {label}? Se sim, qual nivel pode comprovar?",
            "certification" => $"Voce possui a certificacao {label} ou equivalente comprovavel?",
            "education" => $"Voce possui formacao ou curso comprovavel relacionado a {label}?",
            "domain" => $"Voce tem experiencia real no dominio {label}? Em qual contexto?",
            _ => $"Voce possui experiencia real e comprovavel com {label}?"
        };
    }

    public static string SectionPreview(ResumeSection? section) =>
        section is null ? "" : ExtractTextFromJson(section.ContentJson);

    public static string ExtractTextFromJson(string json)
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

    public static void CollectJsonText(JsonElement element, List<string> parts)
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

    public static string SectionDiffStatus(string beforeText, string afterText)
    {
        var before = NormalizeForDiff(beforeText);
        var after = NormalizeForDiff(afterText);
        if (string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(after)) return "added";
        if (!string.IsNullOrWhiteSpace(before) && string.IsNullOrWhiteSpace(after)) return "removed";
        return before.Equals(after, StringComparison.OrdinalIgnoreCase) ? "unchanged" : "changed";
    }

    public static int SectionDiffOrder(string sectionType) => sectionType.ToLowerInvariant() switch
    {
        "identity" => 10,
        "target" => 20,
        "summary" => 30,
        "skills" => 40,
        "experience" => 50,
        "projects" => 60,
        "education" => 70,
        "certifications" => 75,
        "languages" => 78,
        "checkpoints" => 80,
        "plain_text" => 90,
        "raw_import" => 95,
        _ => 100
    };

    public static string SectionDiffTitle(string sectionType) => sectionType.ToLowerInvariant() switch
    {
        "identity" => "Identidade",
        "target" => "Vaga alvo",
        "summary" => "Resumo",
        "skills" => "Competencias",
        "checkpoints" => "Pendencias",
        "plain_text" => "Texto ATS",
        "raw_import" => "Texto importado",
        _ => sectionType
    };

    public static string NormalizeForDiff(string value) =>
        string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    public static string Preview(string value)
    {
        var normalized = NormalizeForDiff(value);
        return normalized.Length <= 260 ? normalized : $"{normalized[..260].Trim()}...";
    }

    public static ScoreReport BuildScoreReport(ResumeEvidenceMapDetail map, TailoringAnalysis analysis)
    {
        var total = Math.Max(1, map.Items.Length);
        var proven = map.Items.Count(x => x.MatchStatus == "proven");
        var adjacent = map.Items.Count(x => x.MatchStatus == "adjacent");
        var blocked = map.Items.Count(x => x.MatchStatus is "missing" or "unknown");
        var mustHaveBlocked = map.Items.Count(x => x.RequirementImportance == "must_have" && x.MatchStatus is "missing" or "unknown");

        var evidence = ClampScore((int)Math.Round(map.EvidenceScore * 0.45m), 45);
        var coverage = ClampScore((int)Math.Round((decimal)proven / total * 20m), 20);
        var related = ClampScore((int)Math.Round((decimal)adjacent / total * 10m), 10);
        var confidence = ClampScore((int)Math.Round(map.Confidence * 10m), 10);
        var riskPenalty = Math.Min(15, blocked * 3 + mustHaveBlocked * 4 + analysis.Checkpoints.Length);
        var readiness = ClampScore(15 - riskPenalty, 15);

        var components = new[]
        {
            Component("evidence", "Evidencia direta", evidence, 45, $"Mapa de evidencia geral: {map.EvidenceScore}/100."),
            Component("coverage", "Cobertura de requisitos", coverage, 20, $"{proven} de {total} requisito(s) com evidencia direta."),
            Component("related", "Aderencia relacionada", related, 10, $"{adjacent} requisito(s) com evidencia adjacente, sem afirmar como experiencia direta."),
            Component("confidence", "Confianca da analise", confidence, 10, $"Confianca do mapa: {map.Confidence:0.00}."),
            Component("readiness", "Risco de revisao", readiness, 15, $"{blocked} requisito(s) ausente(s)/desconhecido(s), {mustHaveBlocked} must-have bloqueado(s), {analysis.Checkpoints.Length} checkpoint(s).")
        };

        return new ScoreReport(components.Sum(x => x.Score), components);
    }

    public static ResumeScoreComponent Component(string key, string label, int score, int maxScore, string reason) =>
        new(key, label, score, maxScore, ScoreStatus(score, maxScore), reason);

    public static int ClampScore(int score, int maxScore) => Math.Clamp(score, 0, maxScore);

    public static string ScoreStatus(int score, int maxScore)
    {
        if (maxScore <= 0) return "neutral";
        var ratio = (decimal)score / maxScore;
        return ratio >= 0.75m ? "good" : ratio >= 0.45m ? "warn" : "bad";
    }

    public static ResumeVersionDetail ToVersionDetail(ResumeVersion version) =>
        new(
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

    public static void AddSection(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, object content) =>
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

    public static string BuildSummary(CandidateProfile? profile, JobTarget target, TailoringAnalysis analysis, JsonElement? identity)
    {
        var targetTitle = CleanTargetRole(target);
        var targetSpecificTitle = ResumeTextSanitizer.CleanProfessionalTitle(target.Title);
        var baseSummary = ResumeTextSanitizer.CleanSummaryText(profile?.ProfessionalSummary ?? "", profile, identity);
        baseSummary = ResumeTextSanitizer.FirstNonEmpty(baseSummary, $"Profissional com foco em {ResumeTextSanitizer.FirstNonEmpty(targetTitle, "tecnologia")}.");
        var sentences = Regex.Split(baseSummary, @"(?<=[.!?])\s+")
            .Select(sentence => sentence.Trim())
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Where(sentence => !ResumeTextSanitizer.LooksLikeSkillListSentence(sentence, analysis.IncludedKeywords))
            .Take(2)
            .ToList();
        if (sentences.Count == 0)
            sentences.Add($"Profissional com foco em {ResumeTextSanitizer.FirstNonEmpty(targetTitle, profile?.TargetRole, "tecnologia")}, desenvolvimento de software e entrega de soluções para uso real.");

        if (!string.IsNullOrWhiteSpace(targetSpecificTitle) &&
            !targetSpecificTitle.Equals(targetTitle, StringComparison.OrdinalIgnoreCase) &&
            !ResumeTextSanitizer.ContainsTerm(string.Join(" ", sentences), targetSpecificTitle))
        {
            sentences.Add($"Foco profissional em {targetSpecificTitle}, com experiência baseada nas evidências do currículo.");
        }

        var focusSentence = BuildTargetFocusSentence(analysis, string.Join(" ", sentences));
        if (!string.IsNullOrWhiteSpace(focusSentence))
        {
            if (sentences.Count > 0)
                sentences[^1] = EnsureTerminalPunctuation(sentences[^1]);
            sentences.Add(focusSentence);
        }

        return string.Join(" ", sentences).Trim();
    }

    private static string EnsureTerminalPunctuation(string value)
    {
        var text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || Regex.IsMatch(text, @"[.!?]$")) return text;
        return $"{text}.";
    }

    private static string BuildTargetFocusSentence(TailoringAnalysis analysis, string currentSummary)
    {
        var provenKeywords = ResumeTextSanitizer.NormalizeSkillList(
                analysis.ProvenEvidence.Select(x => x.Keyword).Concat(analysis.IncludedKeywords))
            .Where(keyword => !ResumeTextSanitizer.ContainsTerm(currentSummary, keyword))
            .Take(6)
            .ToArray();

        if (provenKeywords.Length == 0) return "";

        return $"Experiência comprovada em {NaturalList(provenKeywords)}, aplicada a sistemas corporativos, integrações e evolução de aplicações.";
    }

    private static string NaturalList(string[] values)
    {
        var items = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return items.Length switch
        {
            0 => "",
            1 => items[0],
            2 => $"{items[0]} e {items[1]}",
            _ => $"{string.Join(", ", items[..^1])} e {items[^1]}"
        };
    }

    public static string BuildTitle(JobTarget target)
    {
        var role = ResumeTextSanitizer.FirstNonEmpty(CleanTargetRole(target), "vaga alvo");
        var company = ResumeTextSanitizer.FirstNonEmpty(target.Company);
        return string.IsNullOrWhiteSpace(company)
            ? $"Curriculo direcionado - {role}"
            : $"Curriculo direcionado - {role} - {company}";
    }
}

internal sealed record TailoringAnalysis(string[] IncludedKeywords, string[] AdjacentKeywords, string[] ExcludedKeywords, string[] Checkpoints, EvidenceLine[] ProvenEvidence, EvidenceLine[] AdjacentEvidence);
internal sealed record EvidenceLine(string Keyword, string Text, string Source);
