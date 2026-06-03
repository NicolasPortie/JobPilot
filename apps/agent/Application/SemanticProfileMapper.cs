using System.Globalization;
using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Mapeia um campo de formulário para um valor do perfil do candidato
/// usando inferência semântica de tipo (não só keyword matching).
/// </summary>
public sealed class SemanticProfileMapper
{
    private readonly FieldTypeClassifier _classifier = new();

    public FieldTypeClassifier Classifier => _classifier;

    /// <summary>
    /// Mapeia um campo para o valor do perfil, considerando o tipo inferido e o contexto da sessão.
    /// Retorna null se não houver mapeamento confiável.
    /// </summary>
    public string? Map(ElementCandidate field, CandidateProfile profile, SessionContext ctx, List<ReusableAnswer> answers)
    {
        var inferredType = _classifier.Infer(field);
        var confidence = _classifier.Confidence(field, inferredType);

        // Se confiança é muito baixa, tenta fallback por keyword matching
        if (inferredType == SemanticFieldType.Unknown || confidence < 0.25m)
        {
            inferredType = InferByKeywordFallback(field.Label + " " + field.Name);
            confidence = 0.3m;
        }

        if (inferredType == SemanticFieldType.Unknown) return null;

        var value = ResolveByType(inferredType, field, profile, answers);
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Verifica se esse valor já foi usado em um campo não-relacionado
        if (ctx.WasValueUsed(value, inferredType))
        {
            // Tenta encontrar uma alternativa
            var alternative = FindAlternativeValue(inferredType, field, profile, answers, ctx);
            if (alternative is not null) return alternative;
            return null; // Evita duplicata
        }

        return value;
    }

    /// <summary>
    /// Tenta encontrar um valor alternativo para um tipo quando o principal já foi usado.
    /// </summary>
    private string? FindAlternativeValue(SemanticFieldType type, ElementCandidate field, CandidateProfile profile, List<ReusableAnswer> answers, SessionContext ctx)
    {
        return type switch
        {
            SemanticFieldType.FirstName when ctx.WasValueUsed(profile.FullName, SemanticFieldType.FullName)
                => ExtractFirstName(profile.FullName),
            SemanticFieldType.LastName when ctx.WasValueUsed(profile.FullName, SemanticFieldType.FullName)
                => ExtractLastName(profile.FullName),
            _ => null
        };
    }

    private string? ResolveByType(SemanticFieldType type, ElementCandidate field, CandidateProfile profile, List<ReusableAnswer> answers)
    {
        return type switch
        {
            SemanticFieldType.Cpf => FormatCpf(profile.Cpf),
            SemanticFieldType.Phone => FormatPhone(profile.Phone, field.Placeholder, field.DataMask),
            SemanticFieldType.Email => profile.Email,
            SemanticFieldType.FullName => profile.FullName,
            SemanticFieldType.FirstName => ExtractFirstName(profile.FullName),
            SemanticFieldType.LastName => ExtractLastName(profile.FullName),
            SemanticFieldType.City => profile.City,
            SemanticFieldType.Country => profile.Country,
            SemanticFieldType.LinkedInUrl => profile.LinkedinUrl,
            SemanticFieldType.GitHubUrl => profile.GithubUrl,
            SemanticFieldType.PortfolioUrl => profile.PortfolioUrl,
            SemanticFieldType.Salary => NormalizeSalary(profile.SalaryExpectation, field),
            SemanticFieldType.ResumeUpload => File.Exists(profile.ResumePath) ? profile.ResumePath : null,
            SemanticFieldType.ExperienceYears => TryReusableAnswer(type, field.Label, answers),
            _ => TryReusableAnswer(type, field.Label, answers)
        };
    }

    private static string? TryReusableAnswer(SemanticFieldType type, string label, List<ReusableAnswer> answers)
    {
        var normalized = label.ToLowerInvariant();
        return answers.FirstOrDefault(a =>
            normalized.Contains(a.QuestionKey.ToLowerInvariant()) ||
            normalized.Contains(a.QuestionText.ToLowerInvariant()))?.AnswerValue;
    }

    private static SemanticFieldType InferByKeywordFallback(string text)
    {
        var txt = text.ToLowerInvariant();
        if (AgentText.ContainsAny(txt, ["cpf"])) return SemanticFieldType.Cpf;
        if (AgentText.ContainsAny(txt, ["email", "e-mail"])) return SemanticFieldType.Email;
        if (AgentText.ContainsAny(txt, ["telefone", "fone", "celular", "phone"])) return SemanticFieldType.Phone;
        if (AgentText.ContainsAny(txt, ["nome completo", "full name"])) return SemanticFieldType.FullName;
        if (AgentText.ContainsAny(txt, ["primeiro nome", "first name"])) return SemanticFieldType.FirstName;
        if (AgentText.ContainsAny(txt, ["sobrenome", "last name"])) return SemanticFieldType.LastName;
        if (AgentText.ContainsAny(txt, ["cidade", "city", "location", "localizacao", "local"])) return SemanticFieldType.City;
        if (AgentText.ContainsAny(txt, ["linkedin"])) return SemanticFieldType.LinkedInUrl;
        if (AgentText.ContainsAny(txt, ["github"])) return SemanticFieldType.GitHubUrl;
        if (AgentText.ContainsAny(txt, ["portfolio", "website"])) return SemanticFieldType.PortfolioUrl;
        if (AgentText.ContainsAny(txt, ["pretensao", "salary", "remuneracao"])) return SemanticFieldType.Salary;
        return SemanticFieldType.Unknown;
    }

    /// <summary>
    /// Formata CPF mantendo pontos e hífen se o placeholder/mask os usa,
    /// ou só dígitos se o campo parece aceitar só números.
    /// </summary>
    public static string FormatCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return "";
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        if (digits.Length != 11) return cpf; // já formatado ou inválido
        return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..11]}";
    }

    /// <summary>
    /// Formata telefone baseado na máscara do placeholder ou data-mask.
    /// </summary>
    public static string FormatPhone(string phone, string placeholder, string dataMask)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 10) return phone;

        // Se a máscara tem DDD com parênteses
        var mask = (placeholder + dataMask).ToLowerInvariant();
        if (mask.Contains("(") && mask.Contains(")"))
        {
            if (digits.Length == 11)
                return $"({digits[..2]}) {digits[2..7]}-{digits[7..11]}";
            if (digits.Length == 10)
                return $"({digits[..2]}) {digits[2..6]}-{digits[6..10]}";
        }

        // Se a máscara usa formato internacional
        if (mask.Contains("+") || mask.Contains("country"))
        {
            if (digits.Length == 13)
                return $"+{digits[..2]} {digits[2..4]} {digits[4..9]}-{digits[9..13]}";
        }

        return phone;
    }

    public static string ExtractFirstName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "";
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault() ?? fullName;
    }

    public static string ExtractLastName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "";
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : "";
    }

    /// <summary>
    /// Normaliza salário: se o campo parece aceitar só número (inputmode=numeric, pattern com dígitos),
    /// remove R$ e pontos. Senão mantém formato original.
    /// </summary>
    public static string NormalizeSalary(string salary, ElementCandidate field)
    {
        if (string.IsNullOrWhiteSpace(salary)) return "";

        var cleaned = salary.Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Trim();

        var comma = cleaned.LastIndexOf(',');
        var dot = cleaned.LastIndexOf('.');
        var decimalIndex = Math.Max(comma, dot);
        if (decimalIndex >= 0 && cleaned.Length - decimalIndex - 1 is > 0 and <= 2)
            cleaned = cleaned[..decimalIndex];

        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return "";

        var mask = $"{field.Placeholder} {field.DataMask}".ToLowerInvariant();
        if ((mask.Contains("0.000,00") || mask.Contains("#.###,##")) &&
            !salary.Any(char.IsLetter) &&
            digits.Length is >= 3 and <= 6)
        {
            return $"{digits}000";
        }

        if ((mask.Contains("0,00") || mask.Contains("r$")) &&
            !salary.Any(char.IsLetter) &&
            digits.Length is >= 3 and <= 6)
        {
            return $"{digits}00";
        }

        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            ? amount.ToString(CultureInfo.InvariantCulture)
            : digits;
    }
}
