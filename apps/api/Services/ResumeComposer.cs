using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
/// <see cref="ResumeModel"/> ou de seções salvas. Único ponto que decide formato e limites por layout.
/// </summary>
internal static class ResumeComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Seções que descrevem a versão, não o candidato: nunca entram no texto do currículo.</summary>
    private static readonly HashSet<string> NonContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "identity", "target", "checkpoints", "raw_import", "plain_text"
    };

    private static readonly HashSet<string> StructuredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "skills", "experience", "projects", "education", "certifications", "languages"
    };


    public static string Render(ResumeModel model, string layout)
    {
        var limits = Limits.For(layout);
        var sb = new StringBuilder();
        AppendHeader(sb, model.FullName, model.Headline, model.ContactLine, model.LinksLine);
        sb.Append(SummaryBlock(model.Summary, limits));
        sb.Append(SkillsBlock(model.Skills, limits));
        sb.Append(ItemsBlock("experience", model.Experience, limits));
        sb.Append(ItemsBlock("projects", model.Projects, limits));
        sb.Append(ItemsBlock("education", model.Education, limits));
        sb.Append(ItemsBlock("certifications", model.Certifications, limits));
        sb.Append(ItemsBlock("languages", model.Languages, limits));
        return Tidy(sb.ToString());
    }

    /// <summary>
    /// Renderiza as seções salvas de uma versão na ordem em que estão. Seções de metadados
    /// (vaga alvo, pendências, texto importado, texto final) ficam de fora.
    /// </summary>
    public static string RenderSections(IEnumerable<ResumeSection> sections, string layout)
    {
        var ordered = sections.OrderBy(x => x.SortOrder).ToList();
        var limits = Limits.For(layout);
        var identity = ResumeTextSanitizer.SectionObject(ordered, "identity");
        var sb = new StringBuilder();
        AppendHeader(
            sb,
            ResumeTextSanitizer.JsonValue(identity, "fullName"),
            ResumeTextSanitizer.JsonValue(identity, "headline"),
            ResumeTextSanitizer.BuildContactLine(null, identity),
            ResumeTextSanitizer.JoinUniqueNonEmpty(" | ",
                ResumeTextSanitizer.JsonValue(identity, "linkedinUrl"),
                ResumeTextSanitizer.JsonValue(identity, "githubUrl"),
                ResumeTextSanitizer.JsonValue(identity, "portfolioUrl")));

        var bodyStart = sb.Length;
        foreach (var section in ordered)
        {
            if (NonContentTypes.Contains(section.SectionType)) continue;
            switch (section.SectionType.ToLowerInvariant())
            {
                case "summary":
                    sb.Append(SummaryBlock(ResumeTextSanitizer.SectionText([section], "summary"), limits));
                    break;
                case "skills":
                    sb.Append(SkillsBlock(SkillItems(section), limits));
                    break;
                case "experience" or "projects" or "education" or "certifications" or "languages":
                    sb.Append(ItemsBlock(section.SectionType.ToLowerInvariant(), ResumeTextSanitizer.SectionItems([section], section.SectionType), limits));
                    break;
                default:
                    var text = ResumeTailoringAnalyzer.ExtractTextFromJson(section.ContentJson);
                    sb.Append(Block(string.IsNullOrWhiteSpace(section.Title) ? section.SectionType.ToUpperInvariant() : section.Title.ToUpperInvariant(), text));
                    break;
            }
        }
        return sb.Length == bodyStart ? "" : Tidy(sb.ToString());
    }

    /// <summary>True quando há seções de conteúdo estruturado para recompor o texto.</summary>
    public static bool HasStructuredContent(IEnumerable<ResumeSection> sections) =>
        sections.Any(x => StructuredTypes.Contains(x.SectionType));


    public static ResumeModel FromParsed(ParsedResume parsed, string summary, string[] skills, string contactLine, string linksLine, string fullName) =>
        new(fullName, parsed.Headline, contactLine, linksLine, summary, skills,
            parsed.Experience, parsed.Projects, parsed.Education, parsed.Certifications, parsed.Languages);

    /// <summary>Lê as seções estruturadas de uma versão salva; seções ausentes ficam vazias.</summary>
    public static ResumeModel FromVersion(CandidateProfile? profile, ResumeVersion version)
    {
        var sections = version.Sections.OrderBy(x => x.SortOrder).ToList();
        var identity = ResumeTextSanitizer.SectionObject(sections, "identity");
        var fullName = ResumeTextSanitizer.FirstNonEmpty(profile?.FullName, ResumeTextSanitizer.JsonValue(identity, "fullName"));
        var summary = ResumeTextSanitizer.SectionText(sections, "summary");
        if (string.IsNullOrWhiteSpace(summary)) summary = profile?.ProfessionalSummary ?? "";
        if (ResumeProfileInference.IsPollutedSummary(summary))
            summary = ResumeTextSanitizer.CleanSummaryText(summary, profile, identity);

        var experience = ResumeTextSanitizer.SectionItems(sections, "experience");
        var projects = ResumeTextSanitizer.SectionItems(sections, "projects");
        var education = ResumeTextSanitizer.SectionItems(sections, "education");
        var certifications = ResumeTextSanitizer.SectionItems(sections, "certifications");
        var languages = ResumeTextSanitizer.SectionItems(sections, "languages");

        // Bases importadas antes do parser estruturado só têm o histórico no texto corrido.
        if (experience.Count + projects.Count + education.Count + certifications.Count + languages.Count == 0 &&
            !string.IsNullOrWhiteSpace(version.PlainText))
        {
            var legacy = ResumeStructureParser.Parse(version.PlainText);
            (experience, projects, education, certifications, languages) =
                (legacy.Experience, legacy.Projects, legacy.Education, legacy.Certifications, legacy.Languages);
        }

        return new ResumeModel(
            fullName,
            ResumeTextSanitizer.JsonValue(identity, "headline"),
            ResumeTextSanitizer.BuildContactLine(profile, identity),
            ResumeTextSanitizer.JoinUniqueNonEmpty(" | ",
                profile?.LinkedinUrl, ResumeTextSanitizer.JsonValue(identity, "linkedinUrl"),
                profile?.GithubUrl, ResumeTextSanitizer.JsonValue(identity, "githubUrl"),
                profile?.PortfolioUrl, ResumeTextSanitizer.JsonValue(identity, "portfolioUrl")),
            summary,
            ResumeDraftBuilder.ExtractSkillsFromVersion(profile, version),
            experience, projects, education, certifications, languages);
    }

    /// <summary>Seções salvas equivalentes a um modelo (identidade, resumo, competências e itens).</summary>
    public static List<ResumeSection> ToSections(Guid userId, Guid versionId, CandidateProfile? profile, ResumeModel model)
    {
        var sections = new List<ResumeSection>();
        Add(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            fullName = ResumeTextSanitizer.FirstNonEmpty(model.FullName, profile?.FullName),
            email = profile?.Email ?? "",
            phone = profile?.Phone ?? "",
            city = profile?.City ?? "",
            state = profile?.State ?? "",
            country = profile?.Country ?? "",
            linkedinUrl = profile?.LinkedinUrl ?? "",
            githubUrl = profile?.GithubUrl ?? "",
            portfolioUrl = profile?.PortfolioUrl ?? "",
            headline = model.Headline
        });
        if (!string.IsNullOrWhiteSpace(model.Summary)) Add(sections, userId, versionId, "summary", "Resumo", 20, new { text = model.Summary });
        if (model.Skills.Length > 0) Add(sections, userId, versionId, "skills", "Competencias", 30, new { items = model.Skills });
        AddItems(sections, userId, versionId, "experience", "Experiencia", 40, model.Experience);
        AddItems(sections, userId, versionId, "projects", "Projetos", 50, model.Projects);
        AddItems(sections, userId, versionId, "education", "Educacao", 60, model.Education);
        AddItems(sections, userId, versionId, "certifications", "Certificacoes", 70, model.Certifications);
        AddItems(sections, userId, versionId, "languages", "Idiomas", 75, model.Languages);
        return sections;
    }

    public static void Add(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, object content) =>
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

    private static void AddItems(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, List<Dictionary<string, string>> items)
    {
        if (items.Count > 0) Add(sections, userId, versionId, type, title, order, new { items });
    }


    private sealed record Limits(int Summary, int Skills, int Experience, int Projects, int ExperienceBullets, int ProjectBullets)
    {
        public static Limits For(string layout) => layout == "ats_compact"
            ? new Limits(520, 24, 4, 2, 5, 3)
            : new Limits(850, 36, 7, 4, 8, 5);
    }

    private static void AppendHeader(StringBuilder sb, string fullName, string headline, string contact, string links)
    {
        ResumeTextSanitizer.Append(sb, fullName, "Nome não informado");
        ResumeTextSanitizer.Append(sb, headline);
        ResumeTextSanitizer.Append(sb, contact);
        ResumeTextSanitizer.Append(sb, links);
        sb.AppendLine();
    }

    private static string SummaryBlock(string summary, Limits limits) =>
        Block("RESUMO PROFISSIONAL", ResumeTextSanitizer.LimitText(summary ?? "", limits.Summary));

    private static string SkillsBlock(string[] skills, Limits limits) =>
        Block("COMPETÊNCIAS", string.Join(", ", skills.Take(limits.Skills)));

    private static string ItemsBlock(string type, List<Dictionary<string, string>> items, Limits limits)
    {
        switch (type)
        {
            case "experience":
                return Entries("EXPERIÊNCIA PROFISSIONAL", items.Take(limits.Experience), ExperienceHeading, limits.ExperienceBullets);
            case "projects":
                return Entries("PROJETOS", items.Take(limits.Projects), ProjectHeading, limits.ProjectBullets);
            case "education":
                return Entries("FORMAÇÃO", items, EducationHeading, 0);
            case "certifications":
                return Bullets("CERTIFICAÇÕES", items.Select(CertificationLine));
            default:
                return Bullets("IDIOMAS", items.Select(LanguageLine));
        }
    }

    private static string Block(string title, string content) =>
        string.IsNullOrWhiteSpace(content) ? "" : $"{title}\n{content.Trim()}\n\n";

    private static string Entries(string title, IEnumerable<Dictionary<string, string>> items, Func<Dictionary<string, string>, string> heading, int maxBullets)
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
        return Block(title, string.Join("\n", blocks));
    }

    private static string Bullets(string title, IEnumerable<string> lines) =>
        Block(title, string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => $"- {line}")));

    private static string[] SkillItems(ResumeSection section)
    {
        var root = ResumeTextSanitizer.SectionObject([section], "skills");
        if (root is null) return [];
        // Formato antigo de versões adaptadas guardava proven/adjacent/excluded; só o comprovado é currículo.
        var items = ResumeTextSanitizer.SectionArray([section], "skills", "items");
        if (items.Length == 0) items = ResumeTextSanitizer.SectionArray([section], "skills", "proven");
        if (items.Length == 0)
        {
            var text = ResumeTextSanitizer.SectionText([section], "skills");
            items = text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return items;
    }


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
        Regex.Replace(text.Replace("\r\n", "\n"), @"\n{3,}", "\n\n").Trim();
}
