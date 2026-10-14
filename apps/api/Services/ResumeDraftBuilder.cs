using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

/// <summary>Currículo ATS geral (sem vaga): reorganiza o que já está na versão base, sem acrescentar fatos.</summary>
internal static class ResumeDraftBuilder
{
    private static ResumeScoreComponent Component(string key, string label, int score, int maxScore, string reason) =>
        new(key, label, score, maxScore, ScoreStatus(score, maxScore), reason);

    private static string ScoreStatus(int score, int maxScore)
    {
        if (maxScore <= 0) return "neutral";
        var ratio = (decimal)score / maxScore;
        return ratio >= 0.75m ? "good" : ratio >= 0.45m ? "attention" : "risk";
    }

    public static ResumeModel BuildGenericAtsModel(CandidateProfile? profile, ResumeVersion baseVersion)
    {
        var model = ResumeComposer.FromVersion(profile, baseVersion);
        var targetRole = ResumeTextSanitizer.CleanProfessionalTitle(ResumeTextSanitizer.FirstNonEmpty(profile?.TargetRole, baseVersion.TargetRole));
        return string.IsNullOrWhiteSpace(targetRole) ? model : model with { Headline = targetRole };
    }

    public static string BuildGenericAtsPlainText(CandidateProfile? profile, ResumeVersion baseVersion, string layout) =>
        ResumeComposer.Render(BuildGenericAtsModel(profile, baseVersion), layout);

    public static List<ResumeSection> BuildGenericSections(Guid userId, Guid versionId, CandidateProfile? profile, ResumeVersion baseVersion, string plainText)
    {
        var sections = ResumeComposer.ToSections(userId, versionId, profile, BuildGenericAtsModel(profile, baseVersion));
        ResumeComposer.Add(sections, userId, versionId, "plain_text", "Texto ATS", 90, new { text = plainText });
        return sections;
    }

    public static string[] BuildGenericWarnings(string plainText, IEnumerable<ResumeSection> sections)
    {
        var warnings = new List<string>();
        if (!HasSection(sections, plainText, "experience", "EXPERIENCIA PROFISSIONAL"))
            warnings.Add("Secao de experiencia nao foi encontrada de forma estruturada; revise o curriculo base.");
        if (!HasSection(sections, plainText, "skills", "COMPETENCIAS"))
            warnings.Add("Secao de competencias nao foi encontrada de forma estruturada; revise palavras-chave reais.");
        if (plainText.Length < 600)
            warnings.Add("Curriculo ATS geral ficou curto; revise se experiencias, projetos e formacao foram importados.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ScoreReport BuildGenericScoreReport(string plainText, IEnumerable<ResumeSection> sections)
    {
        var sectionList = sections.ToList();
        var structure = 0;
        if (sectionList.Any(x => x.SectionType == "identity")) structure += 10;
        if (HasSection(sectionList, plainText, "summary", "RESUMO PROFISSIONAL")) structure += 10;
        if (HasSection(sectionList, plainText, "skills", "COMPETENCIAS")) structure += 15;
        if (HasSection(sectionList, plainText, "experience", "EXPERIENCIA PROFISSIONAL")) structure += 20;
        if (HasSection(sectionList, plainText, "education", "FORMACAO") || HasSection(sectionList, plainText, "projects", "PROJETOS")) structure += 10;

        var parseSafety = 19;
        if (plainText.Contains('\t')) parseSafety -= 5;
        if (Regex.IsMatch(plainText, @"[│┌┐└┘■●◆★]")) parseSafety -= 8;
        var completeness = plainText.Length >= 1200 ? 13 : plainText.Length >= 700 ? 9 : 5;
        var components = new[]
        {
            Component("structure", "Estrutura ATS", Math.Clamp(structure, 0, 60), 65, "Secoes padrao detectadas e reordenadas em fluxo linear; curriculo geral ainda precisa de revisao humana."),
            Component("parse_safety", "Seguranca de parsing", Math.Clamp(parseSafety, 0, 19), 20, "Uma coluna, texto simples, sem tabelas ou elementos graficos adicionados."),
            Component("completeness", "Completude", completeness, 15, "Volume de conteudo disponivel no curriculo base, sem validar aderencia a uma vaga especifica.")
        };
        return new ScoreReport(components.Sum(x => x.Score), components);
    }

    /// <summary>
    /// Competências da versão. Quando a versão já tem uma lista própria (importada do arquivo ou
    /// editada), ela vale junto com as do perfil; a lista antiga de versões adaptadas (proven) também.
    /// </summary>
    public static string[] ExtractSkillsFromVersion(CandidateProfile? profile, ResumeVersion version)
    {
        var values = new List<string>();
        values.AddRange(profile?.Skills ?? []);
        values.AddRange(ResumeTextSanitizer.SectionArray(version.Sections, "skills", "items"));
        values.AddRange(ResumeTextSanitizer.SectionArray(version.Sections, "skills", "skills"));
        values.AddRange(ResumeTextSanitizer.SectionArray(version.Sections, "skills", "proven"));
        return ResumeTextSanitizer.NormalizeSkillList(
            values.SelectMany(x => x.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }

    public static string ExtractFallbackSection(string source, string[] startLabels, string[] stopLabels, int maxLength)
    {
        var normalized = ResumeTextSanitizer.NormalizeImportedResumeText(source);
        var start = FindLabel(normalized, startLabels, 0);
        if (start.Index < 0) return "";

        var contentStart = start.Index + start.Label.Length;
        var stop = FindLabel(normalized, stopLabels, contentStart);
        var length = stop.Index >= 0 ? stop.Index - contentStart : normalized.Length - contentStart;
        if (length <= 0) return "";

        var content = normalized.Substring(contentStart, length);
        content = Regex.Replace(content, @"(?is)\bPDF hyperlinks:\s*.*$", " ");
        content = ResumeTextSanitizer.CleanItemText(content);
        if (content.Length < 20) return "";
        return ResumeTextSanitizer.LimitText(content, maxLength);
    }

    private static bool HasSection(IEnumerable<ResumeSection> sections, string plainText, string type, string heading) =>
        sections.Any(x => x.SectionType.Equals(type, StringComparison.OrdinalIgnoreCase)) ||
        Regex.IsMatch(RemoveDiacritics(plainText ?? ""), $@"(?im)^\s*{Regex.Escape(RemoveDiacritics(heading))}\s*$");

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    public static (int Index, string Label) FindLabel(string value, string[] labels, int startAt)
    {
        if (startAt < 0 || startAt >= value.Length) return (-1, "");
        var bestIndex = -1;
        var bestLabel = "";
        foreach (var label in labels)
        {
            var match = Regex.Match(value[startAt..], $@"(?i)(^|\n)({Regex.Escape(label)})(\s|:|\n)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            var index = startAt + match.Groups[2].Index;
            if (bestIndex >= 0 && index >= bestIndex) continue;
            bestIndex = index;
            bestLabel = match.Groups[2].Value;
        }
        return (bestIndex, bestLabel);
    }
}

internal sealed record ScoreReport(int Score, ResumeScoreComponent[] Components);
