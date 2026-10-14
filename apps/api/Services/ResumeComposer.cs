using System.Text;
using System.Text.Json;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

/// <summary>Conteúdo de um currículo pronto para virar texto ATS, sem regras de vaga.</summary>
internal sealed record ResumeModel(
    string FullName,
    string Headline,
    string ContactLine,
    string LinksLine,
    string Summary,
    string[] Skills,
    List<Dictionary<string, string>> Experience,
    List<Dictionary<string, string>> Projects,
    List<Dictionary<string, string>> Education,
    List<Dictionary<string, string>> Certifications,
    List<Dictionary<string, string>> Languages);

/// <summary>
/// Renderiza o texto ATS (uma coluna, títulos em maiúsculas, bullets "- ") a partir de um
/// <see cref="ResumeModel"/>. Único ponto que decide formato e limites por layout.
/// </summary>
internal static class ResumeComposer
{
    public static string Render(ResumeModel model, string layout)
    {
        var compact = layout == "ats_compact";
        var sb = new StringBuilder();
        ResumeTextSanitizer.Append(sb, model.FullName, "Nome não informado");
        ResumeTextSanitizer.Append(sb, model.Headline);
        ResumeTextSanitizer.Append(sb, model.ContactLine);
        ResumeTextSanitizer.Append(sb, model.LinksLine);
        sb.AppendLine();

        Section(sb, "RESUMO PROFISSIONAL", ResumeTextSanitizer.LimitText(model.Summary, compact ? 520 : 850));
        Section(sb, "COMPETÊNCIAS", string.Join(", ", model.Skills.Take(compact ? 24 : 36)));
        Entries(sb, "EXPERIÊNCIA PROFISSIONAL", model.Experience.Take(compact ? 4 : 7), ExperienceHeading, compact ? 5 : 8);
        Entries(sb, "PROJETOS", model.Projects.Take(compact ? 2 : 4), ProjectHeading, compact ? 3 : 5);
        Entries(sb, "FORMAÇÃO", model.Education, EducationHeading, 0);
        Bullets(sb, "CERTIFICAÇÕES", model.Certifications.Select(CertificationLine));
        Bullets(sb, "IDIOMAS", model.Languages.Select(LanguageLine));

        return Tidy(sb.ToString());
    }

    public static ResumeModel FromParsed(ParsedResume parsed, string summary, string[] skills, string contactLine, string linksLine, string fullName) =>
        new(fullName, parsed.Headline, contactLine, linksLine, summary, skills,
            parsed.Experience, parsed.Projects, parsed.Education, parsed.Certifications, parsed.Languages);

    /// <summary>Lê as seções estruturadas de uma versão salva; seções ausentes ficam vazias.</summary>
    public static ResumeModel FromVersion(CandidateProfile? profile, ResumeVersion version)
    {
        var sections = version.Sections.OrderBy(x => x.SortOrder).ToList();
        var identity = ResumeTextSanitizer.SectionObject(sections, "identity");
        var fullName = ResumeTextSanitizer.FirstNonEmpty(profile?.FullName, ResumeTextSanitizer.JsonValue(identity, "fullName"));
        var skills = ResumeDraftBuilder.ExtractSkillsFromVersion(profile, version);
        var headline = ResumeTextSanitizer.JsonValue(identity, "headline");
        var summary = ResumeTextSanitizer.SectionText(sections, "summary");
        if (string.IsNullOrWhiteSpace(summary)) summary = profile?.ProfessionalSummary ?? "";

        return new ResumeModel(
            fullName, headline,
            ResumeTextSanitizer.BuildContactLine(profile, identity),
            ResumeTextSanitizer.JoinUniqueNonEmpty(" | ",
                profile?.LinkedinUrl, ResumeTextSanitizer.JsonValue(identity, "linkedinUrl"),
                profile?.GithubUrl, ResumeTextSanitizer.JsonValue(identity, "githubUrl"),
                profile?.PortfolioUrl, ResumeTextSanitizer.JsonValue(identity, "portfolioUrl")),
            summary, skills,
            ResumeTextSanitizer.SectionItems(sections, "experience"),
            ResumeTextSanitizer.SectionItems(sections, "projects"),
            ResumeTextSanitizer.SectionItems(sections, "education"),
            ResumeTextSanitizer.SectionItems(sections, "certifications"),
            ResumeTextSanitizer.SectionItems(sections, "languages"));
    }

    // ───────────────────────── blocos ─────────────────────────

    private static void Section(StringBuilder sb, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.AppendLine(title).AppendLine(content.Trim()).AppendLine();
    }

    private static void Entries(StringBuilder sb, string title, IEnumerable<Dictionary<string, string>> items,
        Func<Dictionary<string, string>, string> heading, int maxBullets)
    {
        var blocks = new List<string>();
        foreach (var item in items)
        {
            var lines = new List<string>();
            var head = heading(item);
            if (!string.IsNullOrWhiteSpace(head)) lines.Add(head);
            var description = ResumeTextSanitizer.Value(item, "description");
            var descriptionLines = description.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in maxBullets > 0 ? descriptionLines.Take(maxBullets) : descriptionLines)
                lines.Add($"- {line.TrimStart('-', ' ')}");
            if (lines.Count > 0) blocks.Add(string.Join("\n", lines));
        }
        if (blocks.Count == 0) return;
        sb.AppendLine(title).AppendLine(string.Join("\n", blocks)).AppendLine();
    }

    private static void Bullets(StringBuilder sb, string title, IEnumerable<string> lines)
    {
        var rendered = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => $"- {line}").ToArray();
        if (rendered.Length == 0) return;
        sb.AppendLine(title).AppendLine(string.Join("\n", rendered)).AppendLine();
    }

    // ───────────────────────── títulos de item ─────────────────────────

    private static string ExperienceHeading(Dictionary<string, string> item)
    {
        var head = ResumeTextSanitizer.JoinNonEmpty(" - ", ResumeTextSanitizer.Value(item, "role"), ResumeTextSanitizer.Value(item, "company"));
        return WithPeriod(head, ResumeTextSanitizer.Value(item, "period"));
    }

    private static string ProjectHeading(Dictionary<string, string> item) =>
        ResumeTextSanitizer.JoinNonEmpty(" - ", ResumeTextSanitizer.Value(item, "name"), ResumeTextSanitizer.Value(item, "role"));

    private static string EducationHeading(Dictionary<string, string> item)
    {
        var head = ResumeTextSanitizer.JoinNonEmpty(" - ", ResumeTextSanitizer.Value(item, "degree"), ResumeTextSanitizer.Value(item, "institution"));
        return WithPeriod(head, ResumeTextSanitizer.Value(item, "year"));
    }

    private static string CertificationLine(Dictionary<string, string> item)
    {
        var meta = ResumeTextSanitizer.JoinNonEmpty(", ", ResumeTextSanitizer.Value(item, "issuer"), ResumeTextSanitizer.Value(item, "year"));
        var name = ResumeTextSanitizer.Value(item, "name");
        return meta.Length == 0 ? name : $"{name} ({meta})";
    }

    private static string LanguageLine(Dictionary<string, string> item)
    {
        var level = ResumeTextSanitizer.Value(item, "level");
        var language = ResumeTextSanitizer.Value(item, "language");
        return level.Length == 0 ? language : $"{language} ({level})";
    }

    private static string WithPeriod(string head, string period) =>
        string.IsNullOrWhiteSpace(period) ? head : string.IsNullOrWhiteSpace(head) ? period : $"{head} | {period}";

    private static string Tidy(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.Replace("\r\n", "\n"), @"\n{3,}", "\n\n").Trim();

    public static string ToJson(object content) => JsonSerializer.Serialize(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
