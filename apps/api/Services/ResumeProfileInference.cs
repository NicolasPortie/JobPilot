using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class ResumeProfileInference
{
    public static ResumeProfileImportData BuildDeterministicImport(string resumeText, string publicText, CandidateProfile profile)
    {
        var parsed = ResumeStructureParser.Parse(resumeText);
        var corpus = $"{resumeText}\n{publicText}";
        var normalized = RemoveDiacritics(corpus).ToLowerInvariant();
        var skills = parsed.Skills
            .Concat(KnownSkills().Where(skill => normalized.Contains(RemoveDiacritics(skill).ToLowerInvariant())))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var email = Regex.Match(corpus, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase).Value;
        var phone = Regex.Match(corpus, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}").Value;
        var links = ExtractLinks(corpus);
        var linkedin = links.FirstOrDefault(x => x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var github = links.FirstOrDefault(x => x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var portfolio = links.FirstOrDefault(x => !x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) && !x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";

        // O resumo vem do próprio currículo; o do perfil só vale se estiver limpo (já houve
        // importações que gravaram nome, telefone e e-mail dentro do resumo).
        var summary = !string.IsNullOrWhiteSpace(parsed.Summary)
            ? parsed.Summary
            : IsPollutedSummary(profile.ProfessionalSummary) || string.IsNullOrWhiteSpace(profile.ProfessionalSummary)
                ? BuildSummary(resumeText)
                : profile.ProfessionalSummary;

        var location = GuessLocation(corpus, profile.City, profile.State);
        return new ResumeProfileImportData(
            string.IsNullOrWhiteSpace(profile.FullName) ? parsed.FullName : profile.FullName,
            email,
            phone,
            location.City,
            location.State,
            "Brasil",
            linkedin,
            github,
            portfolio,
            summary,
            skills,
            string.IsNullOrWhiteSpace(profile.TargetRole) ? ExtractTargetRole(corpus) : profile.TargetRole,
            string.IsNullOrWhiteSpace(profile.Seniority) ? "unknown" : profile.Seniority);
    }

    public static ResumeProfileImportData MergeImportData(ResumeProfileImportData deterministic, ResumeProfileImportData ai)
    {
        return deterministic with
        {
            FullName = Prefer(deterministic.FullName, ai.FullName),
            Email = Prefer(deterministic.Email, ai.Email),
            Phone = Prefer(deterministic.Phone, ai.Phone),
            City = Prefer(deterministic.City, ai.City),
            State = Prefer(deterministic.State, ai.State),
            Country = Prefer(deterministic.Country, ai.Country, "Brasil"),
            LinkedinUrl = Prefer(deterministic.LinkedinUrl, ai.LinkedinUrl),
            GithubUrl = Prefer(deterministic.GithubUrl, ai.GithubUrl),
            PortfolioUrl = Prefer(deterministic.PortfolioUrl, ai.PortfolioUrl),
            ProfessionalSummary = Prefer(ai.ProfessionalSummary, deterministic.ProfessionalSummary),
            Skills = MergeSkills(deterministic.Skills, ai.Skills),
            TargetRole = Prefer(ai.TargetRole, deterministic.TargetRole),
            Seniority = Prefer(ai.Seniority, deterministic.Seniority, "unknown")
        };
    }

    private static string[] KnownSkills() => [
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "SQL", "PostgreSQL", "MySQL",
        "JavaScript", "TypeScript", "React", "HTML", "HTML5", "CSS", "CSS3",
        "Java", "Spring", "Spring Boot", "Node.js", "API REST", "APIs REST",
        "Git", "GitHub", "Docker", "Playwright", "Selenium", "PHP"
    ];

    private static string ExtractTargetRole(string text)
    {
        var normalized = text.Replace("\r", "\n");
        var match = Regex.Match(normalized, @"(?im)^\s*(OBJETIVO|CARGO\s+ALVO|TARGET\s+ROLE|OBJECTIVE)\s*:?\s*$\n\s*(?<role>[^\n|]{3,80})");
        if (!match.Success)
            match = Regex.Match(normalized, @"(?im)^\s*(OBJETIVO|CARGO\s+ALVO|TARGET\s+ROLE|OBJECTIVE)\s*:?\s*(?<role>[^\n|]{3,80})");
        if (!match.Success) return "";

        var role = match.Groups["role"].Value.Trim();
        if (Regex.IsMatch(role, @"[@\d]{4,}")) return "";
        if (Regex.IsMatch(role, @"(?i)\b(email|telefone|linkedin|github|portfolio|resumo|competencias|experiencia)\b")) return "";
        return role.Trim(' ', '-', ':', '|');
    }

    public static string[] MergeSkills(IEnumerable<string> current, IEnumerable<string> imported) =>
        current.Concat(imported)
            .Select(NormalizeSkill)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeSkill(string skill) => skill.Trim() switch
    {
        "Apis REST" => "APIs REST",
        "Api REST" => "API REST",
        "Javascript" => "JavaScript",
        "Typescript" => "TypeScript",
        var value => value
    };

    public static string Prefer(string current, string imported, string fallback = "") =>
        !string.IsNullOrWhiteSpace(current) ? current.Trim() : (!string.IsNullOrWhiteSpace(imported) ? imported.Trim() : fallback);

    // Sem seção de resumo, aproveita a primeira frase longa do texto; nunca inventa uma profissão.
    private static string BuildSummary(string resumeText) =>
        Regex.Split(resumeText ?? "", @"(?<=[.!?])\s+").FirstOrDefault(s => s.Length > 80 && s.Length < 600)?.Trim() ?? "";

    public static bool IsPollutedSummary(string? summary) =>
        !string.IsNullOrWhiteSpace(summary) &&
        (summary.Contains('@') || Regex.IsMatch(summary, @"(\(\d{2}\)\s?|\b\d{2}\s)9?\d{4}[-\s]?\d{4}\b") ||
         Regex.IsMatch(summary, @"(?i)\b(PERFIL PROFISSIONAL|RESUMO PROFISSIONAL|LinkedIn \| GitHub)\b"));

    private static (string City, string State) GuessLocation(string text, string currentCity, string currentState)
    {
        if (!string.IsNullOrWhiteSpace(currentCity) && !string.IsNullOrWhiteSpace(currentState))
            return (currentCity, currentState.ToUpperInvariant());
        var match = Regex.Match(text, @"\b([A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}]+(?:\s+[A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}]+){0,3})\s*[-,\/]\s*(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)\b", RegexOptions.IgnoreCase);
        var city = !string.IsNullOrWhiteSpace(currentCity) ? currentCity : (match.Success ? match.Groups[1].Value.Trim() : "");
        var state = !string.IsNullOrWhiteSpace(currentState) ? currentState : (match.Success ? match.Groups[2].Value.ToUpperInvariant() : "");
        return (city, state);
    }

    public static string[] ExtractLinks(string text) =>
        Regex.Matches(text ?? "", @"https?://[^\s\)\]\}<>""']+", RegexOptions.IgnoreCase)
            .Select(m => NormalizeLink(m.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeLink(string value)
    {
        var link = value.TrimEnd('.', ',', ';');
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Fragment))
            link = link[..^uri.Fragment.Length];
        return link;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}

