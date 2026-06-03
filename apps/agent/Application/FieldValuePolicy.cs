using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public sealed class FieldValuePolicy
{
    private readonly LocalAnswerResolver _resolver;
    private readonly FieldTypeClassifier _classifier = new();

    public FieldValuePolicy(LocalAnswerResolver resolver) => _resolver = resolver;

    public async Task<ResolvedFieldValue?> ResolveTextAsync(ElementCandidate field, AgentTaskResponse task, string jobContext, SessionContext ctx, AgentPolicy policy)
    {
        var resolution = _resolver.ResolveSemantic(field, ctx);
        var value = resolution?.Value;
        var source = SourceFromResolution(resolution);
        var confidence = resolution?.Confidence ?? 0m;
        var inferredType = resolution?.InferredType ?? _classifier.Infer(field);
        var reason = resolution?.Reason ?? "semantic";
        if (inferredType == SemanticFieldType.Unknown)
            inferredType = _classifier.Infer(field);

        if (string.IsNullOrWhiteSpace(value))
        {
            value = _resolver.Resolve(field.SearchText, field.SearchText, jobContext);
            source = string.IsNullOrWhiteSpace(value) ? ValueSources.ManualRequired : ValueSources.ReusableAnswer;
            confidence = string.IsNullOrWhiteSpace(value) ? 0m : 0.5m;
            reason = "legacy-text";
        }

        if (string.IsNullOrWhiteSpace(value) && DecisionEngine.IsSensitiveQuestion(field.SearchText))
            return null;

        if (string.IsNullOrWhiteSpace(value) && inferredType is SemanticFieldType.LinkedInUrl or SemanticFieldType.GitHubUrl or SemanticFieldType.PortfolioUrl)
            return null;

        if (string.IsNullOrWhiteSpace(value) && (field.Required || policy.AutoSubmitMode.Equals("aggressive", StringComparison.OrdinalIgnoreCase)))
        {
            if (IsIntegerExperienceQuestion(field))
            {
                value = ResolveConservativeExperienceYears(field, task.Profile);
                if (string.IsNullOrWhiteSpace(value))
                    return null;
                source = ValueSources.CandidateProfile;
                confidence = 0.54m;
                reason = "numeric-experience-from-profile";
                inferredType = SemanticFieldType.ExperienceYears;
            }
            else
            {
                value = await _resolver.DraftAsync(field.SearchText, jobContext) ?? "";
                source = ValueSources.AiDraft;
                confidence = 0.45m;
                reason = "ai-draft";
            }
        }

        value = NormalizeValueForField(field, value);

        if (string.IsNullOrWhiteSpace(value)) return null;

        return new ResolvedFieldValue(value, source, confidence, inferredType, source == ValueSources.AiDraft,
            $"Mapped field via {reason} (type={inferredType}, conf={confidence:0.00}).");
    }

    public ResolvedFieldValue? ResolveExistingCorrection(ElementCandidate field, CandidateProfile? profile)
    {
        if (profile is null || !field.IsTextEntry || field.IsDisabledState || string.IsNullOrWhiteSpace(field.Value))
            return null;

        var type = _classifier.Infer(field);
        var shouldCorrect = type switch
        {
            SemanticFieldType.Phone => PhoneDigits(field.Value) != PhoneDigits(profile.Phone),
            SemanticFieldType.Email => !string.Equals(field.Value.Trim(), profile.Email.Trim(), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.FullName => !string.Equals(NormalizeComparable(field.Value), NormalizeComparable(profile.FullName), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.FirstName => !string.Equals(NormalizeComparable(field.Value), NormalizeComparable(SemanticProfileMapper.ExtractFirstName(profile.FullName)), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.LastName => !string.Equals(NormalizeComparable(field.Value), NormalizeComparable(SemanticProfileMapper.ExtractLastName(profile.FullName)), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.City => !string.Equals(NormalizeComparable(field.Value), NormalizeComparable(profile.City), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.Country => !string.Equals(NormalizeComparable(field.Value), NormalizeComparable(profile.Country), StringComparison.OrdinalIgnoreCase),
            SemanticFieldType.LinkedInUrl => !SameUrlish(field.Value, profile.LinkedinUrl),
            SemanticFieldType.GitHubUrl => !SameUrlish(field.Value, profile.GithubUrl),
            SemanticFieldType.PortfolioUrl => !SameUrlish(field.Value, profile.PortfolioUrl),
            SemanticFieldType.Salary => !SalaryMatches(field.Value, profile.SalaryExpectation),
            SemanticFieldType.ExperienceYears => !IsShortInteger(field.Value),
            _ => false
        };

        if (!shouldCorrect) return null;

        var value = ResolveProfileOverwriteValue(field, profile);
        if (string.IsNullOrWhiteSpace(value)) return null;

        return new ResolvedFieldValue(value, ValueSources.CandidateProfile, 0.9m, type, false,
            "Observed profile field differs from configured candidate profile; correcting value.");
    }

    public string? ResolveSelectValue(ElementCandidate select, AgentTaskResponse task, string jobContext, SessionContext ctx)
    {
        var resolution = _resolver.ResolveSemantic(select, ctx);
        var answer = resolution?.Value;
        if (string.IsNullOrWhiteSpace(answer))
            answer = _resolver.Resolve(select.SearchText, select.SearchText, jobContext);
        if (string.IsNullOrWhiteSpace(answer))
            answer = InferSelectAnswer(select, task.Profile);

        var option = !string.IsNullOrWhiteSpace(answer)
            ? select.Options.FirstOrDefault(o => OptionMatches(o, answer))
            : null;
        if (!string.IsNullOrWhiteSpace(option))
            return option;
        if (!string.IsNullOrWhiteSpace(answer) && select.Options.Count == 0 && LooksLikeYesNoQuestion(select))
            return NormalizeYesNoAnswer(answer);

        return null;
    }

    public string? ResolveChoiceValue(ElementCandidate choice, string jobContext) =>
        _resolver.Resolve(choice.SearchText, choice.SearchText, jobContext);

    public static bool IsEmptySelectValue(ElementCandidate select) =>
        string.IsNullOrWhiteSpace(select.Value) || LooksLikePlaceholderOption(select.Value);

    public static bool LooksLikePlaceholderOption(string text) =>
        string.IsNullOrWhiteSpace(text) ||
        AgentText.ContainsAny(text, ["selecione", "selecionar", "opcao", "option", "select", "choose", "escolha", "prefer not", "prefiro nao", "--"]);

    private static string? InferSelectAnswer(ElementCandidate select, CandidateProfile? profile)
    {
        if (!(LooksLikeYesNoSelect(select) || LooksLikeYesNoQuestion(select)) || profile is null)
            return null;

        var normalized = AgentText.RemoveDiacritics(select.SearchTextWithHints).ToLowerInvariant();
        var skills = string.Join(' ', profile.Skills ?? []).ToLowerInvariant();
        var skillText = AgentText.RemoveDiacritics($"{skills} {profile.ProfessionalSummary} {profile.TargetRole}");

        if (AgentText.ContainsAny(normalized, ["logica de programacao", "orientacao a objetos", "object oriented", "oop"]))
            return AgentText.ContainsAny(skillText, ["desenvolvedor", "developer", "c#", "csharp", ".net", "asp.net", "java"]) ? "sim" : "nao";

        if (AgentText.ContainsAny(normalized, ["html", "html5", "css", "css3"]))
            return AgentText.ContainsAny(skillText, ["react", "frontend", "front-end", "web", "html", "css"]) ? "sim" : "nao";

        var knownTerms = new[]
        {
            "node", "node.js", "javascript", "typescript", "react", "html", "html5", "css", "css3", "php", "java",
            "spring", "spring boot", "api", "apis", "rest", "api rest", "apis rest",
            "c#", "csharp", ".net", "dotnet", "asp.net", "aspnet",
            "git", "github", "sql", "postgres", "mysql", "docker", "aws", "azure"
        };

        var mentionedTerms = knownTerms.Where(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mentionedTerms.Count == 0)
            return null;

        var hasExperience = mentionedTerms.Any(term => skillText.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (!hasExperience && mentionedTerms.Any(term => term is "git" or "github") && !string.IsNullOrWhiteSpace(profile.GithubUrl))
            hasExperience = true;

        return hasExperience ? "sim" : "nao";
    }

    private static bool LooksLikeYesNoSelect(ElementCandidate select) =>
        select.Options.Any(o => OptionMatches(o, "sim") || OptionMatches(o, "yes")) &&
        select.Options.Any(o => OptionMatches(o, "nao") || OptionMatches(o, "no"));

    private static bool LooksLikeYesNoQuestion(ElementCandidate select) =>
        AgentText.ContainsAny(select.SearchTextWithHints, [
            "voce possui", "você possui", "possui experiencia", "possui experiência",
            "possui conhecimento", "tem experiencia", "tem experiência", "have experience",
            "do you have", "are you experienced"
        ]);

    private static string NormalizeYesNoAnswer(string answer)
    {
        var normalized = AgentText.RemoveDiacritics(answer).Trim().ToLowerInvariant();
        return normalized is "sim" or "yes" or "true" ? "Sim" : "Não";
    }

    public static bool IsIntegerExperienceQuestion(ElementCandidate field) =>
        IsIntegerExperienceQuestionText(field.SearchTextWithHints);

    private static string SourceFromResolution(FieldResolution? resolution)
    {
        if (resolution is null) return ValueSources.ManualRequired;
        if (resolution.Reason.StartsWith("reusable-answer", StringComparison.OrdinalIgnoreCase)) return ValueSources.ReusableAnswer;
        if (resolution.Reason.StartsWith("question-pattern", StringComparison.OrdinalIgnoreCase)) return ValueSources.QuestionPattern;
        if (resolution.Reason.StartsWith("semantic-inference", StringComparison.OrdinalIgnoreCase)) return ValueSources.CandidateProfile;
        return ValueSources.FallbackPolicy;
    }

    private static string ResolveProfileOverwriteValue(ElementCandidate field, CandidateProfile profile)
    {
        var directLabel = $"{field.Label} {field.Name} {field.Placeholder}";
        var directType = InferProfileFieldFromDirectSignals(directLabel);
        if (directType is not SemanticFieldType.Unknown)
            return ResolveProfileValueByType(directType, field, profile);

        return field.SearchTextWithHints switch
        {
            var text when AgentText.ContainsAny(text, ["nome completo", "full name"])
                => profile.FullName,
            var text when AgentText.ContainsAny(text, ["primeiro nome", "first name", "given name"])
                => SemanticProfileMapper.ExtractFirstName(profile.FullName),
            var text when AgentText.ContainsAny(text, ["sobrenome", "last name", "family name", "surname"])
                => SemanticProfileMapper.ExtractLastName(profile.FullName),
            var text when AgentText.ContainsAny(text, ["telefone", "fone", "celular", "whatsapp", "phone", "mobile"])
                => SemanticProfileMapper.FormatPhone(profile.Phone, field.Placeholder, field.DataMask),
            var text when AgentText.ContainsAny(text, ["email", "e-mail"])
                => profile.Email,
            var text when AgentText.ContainsAny(text, ["cidade", "city", "location", "localizacao", "local", "current location", "where are you based"])
                => profile.City,
            var text when AgentText.ContainsAny(text, ["pais", "country"])
                => profile.Country,
            var text when AgentText.ContainsAny(text, ["linkedin"])
                => profile.LinkedinUrl,
            var text when AgentText.ContainsAny(text, ["github"])
                => profile.GithubUrl,
            var text when AgentText.ContainsAny(text, ["portfolio", "portifolio", "website", "site"])
                => profile.PortfolioUrl,
            var text when AgentText.ContainsAny(text, ["pretensao", "salary", "remuneracao", "compensation", "salario"])
                => SemanticProfileMapper.NormalizeSalary(profile.SalaryExpectation, field),
            var text when IsIntegerExperienceQuestionText(text)
                => ResolveConservativeExperienceYears(field, profile) ?? "",
            _ => ""
        };
    }

    private static SemanticFieldType InferProfileFieldFromDirectSignals(string directLabel)
    {
        if (AgentText.ContainsAny(directLabel, ["telefone", "fone", "celular", "whatsapp", "phone", "mobile"]))
            return SemanticFieldType.Phone;
        if (AgentText.ContainsAny(directLabel, ["email", "e-mail"]))
            return SemanticFieldType.Email;
        if (AgentText.ContainsAny(directLabel, ["primeiro nome", "first name", "given name"]))
            return SemanticFieldType.FirstName;
        if (AgentText.ContainsAny(directLabel, ["sobrenome", "last name", "family name", "surname"]))
            return SemanticFieldType.LastName;
        if (AgentText.ContainsAny(directLabel, ["nome completo", "full name"]))
            return SemanticFieldType.FullName;
        if (AgentText.ContainsAny(directLabel, ["cidade", "city", "location", "localizacao", "local", "current location", "where are you based"]))
            return SemanticFieldType.City;
        if (AgentText.ContainsAny(directLabel, ["pais", "country"]))
            return SemanticFieldType.Country;
        if (AgentText.ContainsAny(directLabel, ["linkedin"]))
            return SemanticFieldType.LinkedInUrl;
        if (AgentText.ContainsAny(directLabel, ["github"]))
            return SemanticFieldType.GitHubUrl;
        if (AgentText.ContainsAny(directLabel, ["portfolio", "portifolio", "website", "site"]))
            return SemanticFieldType.PortfolioUrl;
        if (AgentText.ContainsAny(directLabel, ["pretensao", "salary", "remuneracao", "compensation", "salario"]))
            return SemanticFieldType.Salary;
        return SemanticFieldType.Unknown;
    }

    private static string ResolveProfileValueByType(SemanticFieldType type, ElementCandidate field, CandidateProfile profile) =>
        type switch
        {
            SemanticFieldType.Phone => SemanticProfileMapper.FormatPhone(profile.Phone, field.Placeholder, field.DataMask),
            SemanticFieldType.Email => profile.Email,
            SemanticFieldType.FirstName => SemanticProfileMapper.ExtractFirstName(profile.FullName),
            SemanticFieldType.LastName => SemanticProfileMapper.ExtractLastName(profile.FullName),
            SemanticFieldType.FullName => profile.FullName,
            SemanticFieldType.City => profile.City,
            SemanticFieldType.Country => profile.Country,
            SemanticFieldType.LinkedInUrl => profile.LinkedinUrl,
            SemanticFieldType.GitHubUrl => profile.GithubUrl,
            SemanticFieldType.PortfolioUrl => profile.PortfolioUrl,
            SemanticFieldType.Salary => SemanticProfileMapper.NormalizeSalary(profile.SalaryExpectation, field),
            _ => ""
        };

    private string NormalizeValueForField(ElementCandidate field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (IsIntegerExperienceQuestion(field))
        {
            var match = Regex.Match(value, @"\b([0-9]|[1-3][0-9]|40)\b");
            if (match.Success) return ClampExperience(match.Groups[1].Value);
            return "";
        }
        if (_classifier.Infer(field) == SemanticFieldType.Salary)
            return SemanticProfileMapper.NormalizeSalary(value, field);
        return value;
    }

    private static bool IsWholeMoneyAmount(string value)
    {
        var normalized = SemanticProfileMapper.NormalizeSalary(value, new ElementCandidate(
            "salary", "input", "text", "", "salary", "", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0)));
        return !string.IsNullOrWhiteSpace(normalized) && value.Trim() == normalized;
    }

    private static bool SalaryMatches(string current, string expected)
    {
        if (!AgentText.ContainsAny(current, ["R$", "$"]))
            return IsWholeMoneyAmount(current);

        var currentValue = ParseSalaryAmount(current);
        var expectedValue = ParseSalaryAmount(expected);
        return currentValue.HasValue && expectedValue.HasValue && Math.Abs(currentValue.Value - expectedValue.Value) < 0.01m;
    }

    private static decimal? ParseSalaryAmount(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Trim();
        if (cleaned.Contains(','))
            cleaned = cleaned.Replace(".", "").Replace(",", ".");
        else
            cleaned = Regex.Replace(cleaned, @"[^\d.]", "");

        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    private static bool IsIntegerExperienceQuestionText(string text) =>
        AgentText.ContainsAny(text, [
            "quantos anos", "anos de experiencia", "anos de experiência",
            "years of experience", "how many years", "anos voce usa",
            "anos você usa", "anos ja usa", "anos já usa"
        ]);

    private static string? ResolveConservativeExperienceYears(ElementCandidate field, CandidateProfile? profile)
    {
        if (profile is null) return null;

        var normalized = AgentText.RemoveDiacritics(field.SearchTextWithHints).ToLowerInvariant();
        var profileText = AgentText.RemoveDiacritics($"{string.Join(' ', profile.Skills ?? [])} {profile.ProfessionalSummary} {profile.TargetRole}").ToLowerInvariant();

        var knownTerms = new (string[] Terms, string Years)[]
        {
            (["asp.net", "aspnet", ".net", "dotnet", "c#", "csharp"], "3"),
            (["sql", "entity framework", "ef core"], "3"),
            (["javascript", "typescript", "react", "node", "node.js"], "2"),
            (["qa", "tester", "test automation", "playwright", "selenium", "cypress"], "2"),
            (["php"], "1"),
            (["java", "spring"], "0")
        };

        foreach (var (terms, years) in knownTerms)
        {
            if (!terms.Any(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase)))
                continue;

            return terms.Any(term => profileText.Contains(term, StringComparison.OrdinalIgnoreCase))
                ? years
                : "0";
        }

        return null;
    }

    private static bool IsShortInteger(string value) =>
        Regex.IsMatch(value.Trim(), @"^\d{1,2}$");

    private static string ClampExperience(string value)
    {
        if (!int.TryParse(value, out var number)) return "1";
        return Math.Clamp(number, 0, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string PhoneDigits(string value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        return digits.StartsWith("55", StringComparison.Ordinal) && digits.Length > 11 ? digits[2..] : digits;
    }

    private static string NormalizeComparable(string value) =>
        AgentText.RemoveDiacritics(value ?? "").Trim().ToLowerInvariant();

    private static bool SameUrlish(string current, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        var a = NormalizeUrlish(current);
        var b = NormalizeUrlish(expected);
        return a == b || a.EndsWith(b, StringComparison.OrdinalIgnoreCase) || b.EndsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUrlish(string value) =>
        AgentText.RemoveDiacritics(value ?? "")
            .Trim()
            .TrimEnd('/')
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("www.", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

    private static bool OptionMatches(string option, string answer)
    {
        var normalizedOption = AgentText.RemoveDiacritics(option).ToLowerInvariant();
        var normalizedAnswer = AgentText.RemoveDiacritics(answer).ToLowerInvariant();
        if (IsYes(normalizedOption) && IsYes(normalizedAnswer)) return true;
        if (IsNo(normalizedOption) && IsNo(normalizedAnswer)) return true;
        return normalizedOption == normalizedAnswer ||
               normalizedOption.Contains(normalizedAnswer) ||
               normalizedAnswer.Contains(normalizedOption);
    }

    private static bool IsYes(string value) => value.Trim() is "sim" or "yes" or "true";

    private static bool IsNo(string value) => value.Trim() is "nao" or "no" or "false";
}
