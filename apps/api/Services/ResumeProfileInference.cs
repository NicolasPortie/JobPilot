using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class ResumeProfileInference
{
    public static ResumeProfileImportData BuildDeterministicImport(string resumeText, string publicText, CandidateProfile profile)
    {
        var corpus = $"{resumeText}\n{publicText}";
        var normalized = RemoveDiacritics(corpus).ToLowerInvariant();
        var skills = KnownSkills()
            .Where(skill => normalized.Contains(RemoveDiacritics(skill).ToLowerInvariant()))
            .Concat(profile.Skills)
            .Concat(InferRelatedSkills(normalized))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var email = Regex.Match(corpus, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase).Value;
        var phone = Regex.Match(corpus, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}").Value;
        var links = ExtractLinks(corpus);
        var linkedin = links.FirstOrDefault(x => x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var github = links.FirstOrDefault(x => x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var portfolio = links.FirstOrDefault(x => !x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) && !x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";

        var answers = BuildTechnicalAnswers(skills, normalized);
        var summary = string.IsNullOrWhiteSpace(profile.ProfessionalSummary)
            ? BuildSummary(corpus, skills)
            : profile.ProfessionalSummary;

        var location = GuessLocation(corpus, profile.City, profile.State);
        return new ResumeProfileImportData(
            profile.FullName,
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
            string.IsNullOrWhiteSpace(profile.Seniority) ? "unknown" : profile.Seniority,
            answers);
    }

    private static ResumeScreeningAnswer[] BuildTechnicalAnswers(string[] skills, string normalizedCorpus)
    {
        var skillSet = new HashSet<string>(skills, StringComparer.OrdinalIgnoreCase);
        var answers = new List<ResumeScreeningAnswer>();
        AddYesNo(answers, "typescript", "Possui conhecimento/experiência com TypeScript?", HasAny(skillSet, "TypeScript"));
        AddYesNo(answers, "javascript", "Possui conhecimento/experiência com JavaScript?", HasAny(skillSet, "JavaScript", "TypeScript", "React"));
        AddYesNo(answers, "java", "Possui conhecimento sólido da linguagem Java (17+)?", HasAny(skillSet, "Java"));
        AddYesNo(answers, "spring_boot", "Possui conhecimento sólido da base do ecossistema Spring Boot?", HasAny(skillSet, "Spring Boot", "Spring"));
        AddYesNo(answers, "apis_rest", "Possui conhecimento sólido no que se refere a construção de APIs REST?", HasAny(skillSet, "API REST", "APIs REST", "REST", "ASP.NET Core"));
        AddYesNo(answers, "html_css", "Possui conhecimento sólido de HTML5/ CSS3?", HasAny(skillSet, "HTML", "HTML5", "CSS", "CSS3", "React"));
        AddYesNo(answers, "oop", "Possui experiência anterior com lógica de programação e orientação a objetos bem consolidadas?", HasAny(skillSet, "C#", ".NET", "Java", "ASP.NET Core") || normalizedCorpus.Contains("orientacao a objetos"));
        return answers.ToArray();
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
            Seniority = Prefer(ai.Seniority, deterministic.Seniority, "unknown"),
            Answers = deterministic.Answers.Concat(ai.Answers).GroupBy(a => ReusableAnswerService.StableQuestionKey(a.QuestionKey + " " + a.QuestionText)).Select(g => g.Last()).ToArray()
        };
    }

    private static string[] KnownSkills() => [
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "SQL", "PostgreSQL", "MySQL",
        "JavaScript", "TypeScript", "React", "HTML", "HTML5", "CSS", "CSS3",
        "Java", "Spring", "Spring Boot", "Node.js", "API REST", "APIs REST",
        "Git", "GitHub", "Docker", "Playwright", "Selenium", "PHP"
    ];

    private static IEnumerable<string> InferRelatedSkills(string normalized)
    {
        if (normalized.Contains("react")) { yield return "JavaScript"; yield return "HTML5"; yield return "CSS3"; }
        if (normalized.Contains("spring boot")) yield return "Java";
        if (normalized.Contains("asp.net")) { yield return ".NET"; yield return "C#"; yield return "API REST"; }
        if (normalized.Contains("api")) yield return "API REST";
        if (normalized.Contains("typescript")) yield return "JavaScript";
        if (normalized.Contains("javascript")) { yield return "HTML5"; yield return "CSS3"; }
        if (normalized.Contains("java") && normalized.Contains("spring")) yield return "Spring Boot";
    }

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

    private static bool HasAny(HashSet<string> skills, params string[] names) => names.Any(skills.Contains);

    public static bool IsAffirmative(string value) =>
        value.Trim().Equals("sim", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    public static bool IsNegative(string value) =>
        value.Trim().Equals("nao", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("não", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);

    private static void AddYesNo(List<ResumeScreeningAnswer> answers, string key, string question, bool value) =>
        answers.Add(new ResumeScreeningAnswer(key, question, value ? "Sim" : "Não", "resume_import"));

    private static string BuildSummary(string corpus, string[] skills)
    {
        var first = Regex.Split(corpus, @"(?<=[.!?])\s+").FirstOrDefault(s => s.Length > 80 && s.Length < 600) ?? "";
        return string.IsNullOrWhiteSpace(first)
            ? $"Desenvolvedor de Software com experiência em {string.Join(", ", skills.Take(10))}."
            : first.Trim();
    }

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

