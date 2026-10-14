using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static partial class ResumeTailoringAnalyzer
{
    /// <summary>
    /// Rejeita texto de IA que afirme requisito sem evidência ou cite tecnologia que não está
    /// no currículo base, no perfil nem na evidência mapeada.
    /// </summary>
    public static string[] ValidateAiDraft(string plainText, TailoringAnalysis analysis, string evidenceText = "")
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(plainText) || plainText.Length < 120)
        {
            warnings.Add("IA rejeitada: texto gerado curto demais.");
            return warnings.ToArray();
        }

        var factualText = plainText.Split("PENDENCIAS PARA REVISAO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? plainText;
        foreach (var excluded in analysis.ExcludedKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (ResumeTextSanitizer.ContainsTerm(factualText, excluded))
                warnings.Add($"IA rejeitada: requisito sem evidência apareceu como fato: {excluded}.");
        }

        foreach (var adjacent in analysis.AdjacentKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (ResumeTextSanitizer.ContainsTerm(factualText, adjacent) && !ResumeTextSanitizer.ContainsAny(factualText, ["relacionad", "proxim", "adjacent", "similar"]))
                warnings.Add($"IA incluiu evidencia adjacente; revise redacao de: {adjacent}.");
        }

        if (!string.IsNullOrWhiteSpace(evidenceText))
        {
            var allowed = TechnologiesIn(evidenceText);
            allowed.UnionWith(analysis.IncludedKeywords);
            allowed.UnionWith(analysis.AdjacentKeywords);
            foreach (var tech in TechnologiesIn(factualText).Where(tech => !allowed.Contains(tech)).Take(5))
                warnings.Add($"IA rejeitada: tecnologia sem evidência no currículo: {tech}.");
        }

        return warnings.ToArray();
    }

    /// <summary>Tecnologias conhecidas citadas no texto (comparação sem acentos).</summary>
    public static HashSet<string> TechnologiesIn(string text)
    {
        var normalized = JobTargetTextRules.RemoveDiacritics(text ?? "");
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in JobTargetAnalyzer.MatchTechnologies(normalized))
            found.Add(match.Tech);
        return found;
    }

    public static TailoringAnalysis AnalyzeEvidence(ResumeEvidenceMapDetail map)
    {
        var included = map.Items
            .Where(x => x.MatchStatus == "proven")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adjacent = map.Items
            .Where(x => x.MatchStatus == "adjacent")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var excluded = map.Items
            .Where(x => x.MatchStatus is "missing" or "unknown")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var checkpoints = map.Items
            .Where(x => x.MatchStatus is "missing" or "unknown" || (x.MatchStatus == "adjacent" && x.RequirementImportance == "must_have"))
            .Select(x => $"{x.RequirementLabel}: {x.Reason}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var provenEvidence = map.Items
            .Where(x => x.MatchStatus == "proven" && !string.IsNullOrWhiteSpace(x.EvidenceText))
            .Select(x => new EvidenceLine(x.RequirementLabel, x.EvidenceText, x.EvidenceSource))
            .ToArray();
        var adjacentEvidence = map.Items
            .Where(x => x.MatchStatus == "adjacent" && !string.IsNullOrWhiteSpace(x.EvidenceText))
            .Select(x => new EvidenceLine(x.RequirementLabel, x.EvidenceText, x.EvidenceSource))
            .ToArray();
        return new TailoringAnalysis(included, adjacent, excluded, checkpoints, provenEvidence, adjacentEvidence);
    }

    public static string[] BuildWarnings(ResumeEvidenceMapDetail map, TailoringAnalysis analysis)
    {
        var warnings = new List<string>(map.Warnings);
        if (analysis.ExcludedKeywords.Length > 0)
            warnings.Add("Requisitos sem evidência não foram inseridos como competência no currículo direcionado.");
        if (analysis.AdjacentKeywords.Length > 0)
            warnings.Add("Evidências adjacentes foram descritas de forma conservadora, sem afirmar experiência direta.");
        if (map.Confidence < 0.65m)
            warnings.Add("Confiança do mapa de evidência abaixo do ideal; revise antes de aplicar.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ───────────────────────── currículo adaptado ─────────────────────────

    /// <summary>
    /// Currículo da vaga a partir da versão base: mesmo conteúdo, com título alinhado à vaga,
    /// competências comprovadas da vaga primeiro e bullets/projetos mais relevantes antes.
    /// Nenhuma frase nova é escrita; nada que não esteja na base entra.
    /// </summary>
    public static ResumeModel BuildTailoredModel(CandidateProfile? profile, ResumeVersion baseVersion, JobTarget target, TailoringAnalysis analysis)
    {
        var model = ResumeComposer.FromVersion(profile, baseVersion);
        var relevance = new RelevanceMatcher(analysis.IncludedKeywords.Concat(analysis.AdjacentKeywords));
        return model with
        {
            Headline = ResumeTextSanitizer.FirstNonEmpty(CleanTargetRole(target), model.Headline),
            Skills = BuildPrioritizedSkills(model.Skills, target, analysis),
            Experience = model.Experience.Select(item => WithBulletsByRelevance(item, relevance)).ToList(),
            Projects = model.Projects
                .Select((item, index) => (item, index, score: relevance.Score(ItemText(item))))
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.index)
                .Select(x => WithBulletsByRelevance(x.item, relevance))
                .ToList()
        };
    }

    public static string BuildPlainText(CandidateProfile? profile, ResumeVersion baseVersion, JobTarget target, TailoringAnalysis analysis, string layout) =>
        ResumeComposer.Render(BuildTailoredModel(profile, baseVersion, target, analysis), layout);

    /// <summary>Lê de volta um texto (ex.: gerado por IA) para manter as seções estruturadas coerentes com ele.</summary>
    public static ResumeModel ModelFromText(string plainText, ResumeModel fallback)
    {
        var parsed = ResumeStructureParser.Parse(plainText);
        if (!parsed.HasStructure) return fallback;
        return fallback with
        {
            Headline = ResumeTextSanitizer.FirstNonEmpty(parsed.Headline, fallback.Headline),
            Summary = ResumeTextSanitizer.FirstNonEmpty(parsed.Summary, fallback.Summary),
            Skills = parsed.Skills.Length > 0 ? parsed.Skills : fallback.Skills,
            Experience = parsed.Experience.Count > 0 ? parsed.Experience : fallback.Experience,
            Projects = parsed.Projects.Count > 0 ? parsed.Projects : fallback.Projects,
            Education = parsed.Education.Count > 0 ? parsed.Education : fallback.Education,
            Certifications = parsed.Certifications.Count > 0 ? parsed.Certifications : fallback.Certifications,
            Languages = parsed.Languages.Count > 0 ? parsed.Languages : fallback.Languages
        };
    }

    private static string[] BuildPrioritizedSkills(string[] baseSkills, JobTarget target, TailoringAnalysis analysis)
    {
        var proven = new HashSet<string>(analysis.IncludedKeywords, StringComparer.OrdinalIgnoreCase);
        var excluded = new HashSet<string>(ResumeTextSanitizer.NormalizeSkillList(analysis.ExcludedKeywords).Select(SkillKey));

        // Termos que a vaga pede e o candidato comprova, na ordem de importância da vaga.
        var wanted = target.Requirements
            .Where(requirement => proven.Contains(requirement.Label))
            .OrderBy(requirement => ImportanceRank(requirement.Importance))
            .ThenBy(requirement => requirement.SortOrder)
            .Select(requirement => requirement.Label);

        var ordered = new List<string>();
        var seen = new HashSet<string>();
        foreach (var skill in wanted.Concat(baseSkills))
        {
            var key = SkillKey(skill);
            if (key.Length == 0 || excluded.Contains(key) || !seen.Add(key)) continue;
            ordered.Add(skill);
        }
        return ordered.Take(36).ToArray();
    }

    private static int ImportanceRank(string importance) => importance switch { "must_have" => 0, "nice_to_have" => 1, _ => 2 };

    private static string SkillKey(string value) => Regex.Replace(JobTargetTextRules.RemoveDiacritics(value ?? "").ToLowerInvariant(), @"[^a-z0-9#+.]", "");

    private static Dictionary<string, string> WithBulletsByRelevance(Dictionary<string, string> item, RelevanceMatcher relevance)
    {
        var description = ResumeTextSanitizer.Value(item, "description");
        if (string.IsNullOrWhiteSpace(description)) return item;
        var bullets = description.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (bullets.Length < 2) return item;
        var sorted = bullets
            .Select((bullet, index) => (bullet, index, score: relevance.Score(bullet)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Select(x => x.bullet);
        return new Dictionary<string, string>(item, StringComparer.OrdinalIgnoreCase) { ["description"] = string.Join("\n", sorted) };
    }

    private static string ItemText(Dictionary<string, string> item) => string.Join(" ", item.Values);

    /// <summary>Conta quantos termos comprovados/relacionados da vaga aparecem em um trecho.</summary>
    private sealed class RelevanceMatcher
    {
        private readonly Regex[] _terms;

        public RelevanceMatcher(IEnumerable<string> terms) =>
            _terms = terms
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => Regex.Escape(JobTargetTextRules.RemoveDiacritics(term).ToLowerInvariant()))
                .Distinct()
                .Select(term => new Regex($@"(?<![a-z0-9#+]){term}(?![a-z0-9#+])", RegexOptions.CultureInvariant))
                .ToArray();

        public int Score(string text)
        {
            var normalized = JobTargetTextRules.RemoveDiacritics(text ?? "").ToLowerInvariant();
            return _terms.Count(term => term.IsMatch(normalized));
        }
    }

    // ───────────────────────── limpeza do texto final ─────────────────────────

    public static string SanitizeFinalPlainText(string plainText, JobTarget? target = null)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return "";
        var cleanTargetTitle = target is null ? "" : CleanTargetRole(target);
        var targetTitle = target?.Title ?? "";
        var targetCompany = target?.Company ?? "";
        var reachedResumeSection = false;
        var nonEmptyLineCount = 0;
        var lines = plainText
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => string.IsNullOrWhiteSpace(line) || !ResumeTextSanitizer.IsMalformedResumeLine(line.TrimStart('-', ' ')))
            .Select(line =>
            {
                var cleaned = line;
                if (!string.IsNullOrWhiteSpace(targetTitle) && !string.IsNullOrWhiteSpace(cleanTargetTitle))
                    cleaned = cleaned.Replace(targetTitle, cleanTargetTitle, StringComparison.OrdinalIgnoreCase);

                var trimmed = cleaned.Trim();
                if (IsResumeSectionHeading(trimmed))
                    reachedResumeSection = true;

                if (!reachedResumeSection && nonEmptyLineCount <= 5)
                    cleaned = RemoveTargetCompanyFromHeaderLine(cleaned, targetCompany);

                if (!string.IsNullOrWhiteSpace(trimmed))
                    nonEmptyLineCount++;

                return cleaned;
            })
            .ToArray();
        return ResumeTextSanitizer.ApplyPortugueseAccents(string.Join("\n", lines)).Trim();
    }

    private static bool IsResumeSectionHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 48) return false;
        var plain = JobTargetTextRules.RemoveDiacritics(line);
        return Regex.IsMatch(plain, @"(?i)^(RESUMO PROFISSIONAL|COMPETENCIAS|COMPETENCIAS TECNICAS|EXPERIENCIA PROFISSIONAL|PROJETOS|FORMACAO|CERTIFICACOES|IDIOMAS)$");
    }

    public static string CleanTargetRole(JobTarget target)
    {
        var titleWithoutCompany = RemoveTargetCompanyFromHeaderLine(target.Title, target.Company);
        var professionalTitle = ResumeTextSanitizer.CleanProfessionalTitle(titleWithoutCompany);
        var title = Regex.IsMatch(professionalTitle, @"(?i)\b(back\s*-?\s*end|backend|front\s*-?\s*end|frontend|full\s*stack)\b")
            ? ResumeTextSanitizer.CleanResumeHeadlineTitle(professionalTitle)
            : professionalTitle;
        return RemoveTargetCompanyFromHeaderLine(title, target.Company);
    }

    private static string RemoveTargetCompanyFromHeaderLine(string line, string company)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(company)) return line;

        var escapedCompany = Regex.Escape(company.Trim());
        var cleaned = Regex.Replace(line, $@"^\s*{escapedCompany}\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, $@"\s*[\(\[]\s*{escapedCompany}\s*[\)\]]", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, $@"\s*(?:\||-|/|,)\s*{escapedCompany}\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, $@"^\s*{escapedCompany}\s*(?:\||-|/|,)\s*", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, $@"\b(?:na|no|para a|para o|para|da|do)\s+{escapedCompany}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, $@"\b{escapedCompany}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim(' ', '-', '|', ',', '/', ':');
        return cleaned;
    }
}
