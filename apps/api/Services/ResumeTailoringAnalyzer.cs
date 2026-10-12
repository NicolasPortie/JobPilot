using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static partial class ResumeTailoringAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string[] ValidateAiDraft(string plainText, TailoringAnalysis analysis)
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

        return warnings.ToArray();
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

    public static string BuildPlainText(CandidateProfile? profile, ResumeVersion baseVersion, JobTarget target, TailoringAnalysis analysis, string layout)
    {
        var sb = new StringBuilder();
        var identity = ResumeTextSanitizer.SectionObject(baseVersion.Sections, "identity");
        var fullName = ResumeTextSanitizer.FirstNonEmpty(profile?.FullName, ResumeTextSanitizer.JsonValue(identity, "fullName"), ResumeTextSanitizer.JsonValue(identity, "FullName"), "Nome completo nao informado");
        ResumeTextSanitizer.Append(sb, fullName);
        ResumeTextSanitizer.Append(sb, BuildProfessionalHeadline(profile, baseVersion, target));
        ResumeTextSanitizer.Append(sb, ResumeTextSanitizer.BuildContactLine(profile, identity, baseVersion.PlainText));
        ResumeTextSanitizer.Append(sb, ResumeTextSanitizer.JoinUniqueNonEmpty(" | ",
            profile?.LinkedinUrl, ResumeTextSanitizer.JsonValue(identity, "linkedinUrl"), ResumeTextSanitizer.JsonValue(identity, "LinkedinUrl"),
            profile?.GithubUrl, ResumeTextSanitizer.JsonValue(identity, "githubUrl"), ResumeTextSanitizer.JsonValue(identity, "GithubUrl"),
            profile?.PortfolioUrl, ResumeTextSanitizer.JsonValue(identity, "portfolioUrl"), ResumeTextSanitizer.JsonValue(identity, "PortfolioUrl")));
        ResumeTextSanitizer.Blank(sb);

        ResumeTextSanitizer.Section(sb, "RESUMO PROFISSIONAL", BuildSummary(profile, target, analysis, identity));

        var skills = BuildPrioritizedSkills(profile, baseVersion, analysis);
        if (skills.Length > 0) ResumeTextSanitizer.Section(sb, "COMPETENCIAS", string.Join(", ", skills));

        AppendStructuredBaseHistory(sb, baseVersion, layout);

        return ResumeTextSanitizer.ApplyPortugueseAccents(sb.ToString()).Trim();
    }

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
        return Regex.IsMatch(line, @"(?i)^(RESUMO PROFISSIONAL|COMPETENCIAS|COMPETENCIAS TECNICAS|EXPERIENCIA PROFISSIONAL|PROJETOS|FORMACAO|CERTIFICACOES|IDIOMAS)$");
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

    private static string BuildProfessionalHeadline(CandidateProfile? profile, ResumeVersion baseVersion, JobTarget target) =>
        ResumeTextSanitizer.FirstNonEmpty(
            CleanTargetRole(target),
            ResumeTextSanitizer.CleanResumeHeadlineTitle(profile?.TargetRole ?? ""),
            ResumeTextSanitizer.CleanResumeHeadlineTitle(baseVersion.TargetRole),
            "Profissional de Tecnologia");

    private static string[] BuildPrioritizedSkills(CandidateProfile? profile, ResumeVersion baseVersion, TailoringAnalysis analysis)
    {
        var baseSkills = ResumeDraftBuilder.ExtractSkillsFromVersion(profile, baseVersion);
        var proven = ResumeTextSanitizer.NormalizeSkillList(analysis.IncludedKeywords);
        var excluded = new HashSet<string>(ResumeTextSanitizer.NormalizeSkillList(analysis.ExcludedKeywords), StringComparer.OrdinalIgnoreCase);
        var mergedSkills = ResumeTextSanitizer.NormalizeSkillList(proven
            .Concat(baseSkills)
            .Where(skill => !excluded.Contains(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase))
            .Take(36)
            .ToArray();
        return mergedSkills;
    }

    private static void AppendStructuredBaseHistory(StringBuilder sb, ResumeVersion baseVersion, string layout)
    {
        var sections = baseVersion.Sections.OrderBy(x => x.SortOrder).ToList();
        ResumeDraftBuilder.AppendSectionItems(sb, "EXPERIENCIA PROFISSIONAL", ResumeTextSanitizer.SectionItems(sections, "experience"), layout);
        ResumeDraftBuilder.AppendSectionItems(sb, "PROJETOS", ResumeTextSanitizer.SectionItems(sections, "projects"), layout);
        ResumeDraftBuilder.AppendSectionItems(sb, "FORMACAO", ResumeTextSanitizer.SectionItems(sections, "education"), layout);
        ResumeDraftBuilder.AppendSectionItems(sb, "CERTIFICACOES", ResumeTextSanitizer.SectionItems(sections, "certifications"), layout);
        ResumeDraftBuilder.AppendSectionItems(sb, "IDIOMAS", ResumeTextSanitizer.SectionItems(sections, "languages"), layout);

        if (sections.Any(x => x.SectionType is "experience" or "projects" or "education" or "certifications" or "languages"))
            return;

        var basePlainText = baseVersion.PlainText ?? "";
        var max = layout == "ats_compact" ? 900 : 1400;
        ResumeTextSanitizer.Section(sb, "EXPERIENCIA PROFISSIONAL", ResumeDraftBuilder.ExtractFallbackSection(
            basePlainText,
            ["EXPERIENCIA PROFISSIONAL", "EXPERIÊNCIA PROFISSIONAL", "EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "WORK EXPERIENCE"],
            ["PROJETOS", "PROJECTS", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
            max));
        ResumeTextSanitizer.Section(sb, "PROJETOS", ResumeDraftBuilder.ExtractFallbackSection(
            basePlainText,
            ["PROJETOS", "PROJECTS"],
            ["EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
            max / 2));
        ResumeTextSanitizer.Section(sb, "FORMACAO", ResumeDraftBuilder.ExtractFallbackSection(
            basePlainText,
            ["FORMACAO E CERTIFICACOES", "FORMAÇÃO E CERTIFICAÇÕES", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "EDUCATION"],
            ["EXPERIENCIA", "EXPERIÊNCIA", "PROJETOS", "PROJECTS", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
            max / 2));
    }

}
