using System.Text.RegularExpressions;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public class ProfileGuard(
    IRepository<CandidateProfile> profiles,
    IRepository<ProfileValidationRule> rules,
    IUnitOfWork unitOfWork,
    ResumeTextService? resumeTextService = null)
{
    public async Task<List<ProfileValidationIssue>> ValidateAsync(Guid userId)
    {
        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            return [new ProfileValidationIssue("Profile", "Perfil do candidato nao encontrado.", true)];
        }

        var userRules = await EnsureRulesAsync(userId);

        var enriched = EnrichFromResume(profile);
        if (enriched)
        {
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync();
        }

        var issues = new List<ProfileValidationIssue>();
        foreach (var rule in userRules)
        {
            var value = GetFieldValue(profile, rule.FieldName);
            if (!IsValid(value, rule))
            {
                var critical = rule.IsCritical && !rule.FieldName.Equals("Phone", StringComparison.OrdinalIgnoreCase);
                issues.Add(new ProfileValidationIssue(rule.FieldName, rule.ErrorMessage, critical));
            }
        }

        return issues;
    }

    /// <summary>
    /// Completude do perfil (0-100) calculada sobre as regras de validacao do
    /// usuario: quantas passam sobre o total. Mesma fonte de verdade do ValidateAsync.
    /// Somente leitura — nao enriquece nem altera o perfil.
    /// </summary>
    public async Task<(int Passed, int Total)> CompletionAsync(Guid userId)
    {
        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null) return (0, 0);
        var userRules = await EnsureRulesAsync(userId);
        if (userRules.Count == 0) return (0, 0);
        var passed = userRules.Count(rule => IsValid(GetFieldValue(profile, rule.FieldName), rule));
        return (passed, userRules.Count);
    }

    private async Task<List<ProfileValidationRule>> EnsureRulesAsync(Guid userId)
    {
        var userRules = await rules.ListAsync(x => x.UserId == userId);
        if (userRules.Count == 0)
        {
            userRules = GetDefaultRules(userId);
            foreach (var rule in userRules)
                rules.Add(rule);
            await unitOfWork.SaveChangesAsync();
        }

        return userRules;
    }

    private bool EnrichFromResume(CandidateProfile profile)
    {
        var changed = false;
        var resumeText = resumeTextService?.ReadResumeText(profile.ResumePath) ?? "";

        if (string.IsNullOrWhiteSpace(profile.FullName) || profile.FullName.Trim().Equals("Nicolas", StringComparison.OrdinalIgnoreCase))
        {
            var name = ExtractNameFromResume(resumeText, profile.ResumePath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                profile.FullName = name;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.Phone))
        {
            var phone = Regex.Match(resumeText, @"(\+?55\s*)?(\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}");
            if (phone.Success)
            {
                profile.Phone = phone.Value.Trim();
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            var email = Regex.Match(resumeText, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
            if (email.Success)
            {
                profile.Email = email.Value.Trim();
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.LinkedinUrl))
        {
            var linkedin = Regex.Match(resumeText, @"https?://(?:www\.)?linkedin\.com/[^\s]+|linkedin\.com/[^\s]+", RegexOptions.IgnoreCase);
            if (linkedin.Success)
            {
                profile.LinkedinUrl = NormalizeUrl(linkedin.Value);
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.GithubUrl))
        {
            var github = Regex.Match(resumeText, @"https?://(?:www\.)?github\.com/[^\s]+|github\.com/[^\s]+", RegexOptions.IgnoreCase);
            if (github.Success)
            {
                profile.GithubUrl = NormalizeUrl(github.Value);
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.City) || profile.City.Equals("Brazil", StringComparison.OrdinalIgnoreCase) || profile.City.Equals("Brasil", StringComparison.OrdinalIgnoreCase))
        {
            var location = ExtractLocation(resumeText);
            if (!string.IsNullOrWhiteSpace(location.City))
            {
                profile.City = location.City;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(location.State) && string.IsNullOrWhiteSpace(profile.State))
            {
                profile.State = location.State;
                changed = true;
            }
        }
        else if (string.IsNullOrWhiteSpace(profile.State))
        {
            var location = ExtractLocation(resumeText);
            if (!string.IsNullOrWhiteSpace(location.State))
            {
                profile.State = location.State;
                changed = true;
            }
        }

        if (profile.Skills.Length < 3)
        {
            var skills = ExtractSkills(resumeText);
            if (skills.Length > profile.Skills.Length)
            {
                profile.Skills = skills;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.ProfessionalSummary) || profile.ProfessionalSummary.Trim().Length < 80)
        {
            var summary = ExtractProfessionalSummary(resumeText);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                profile.ProfessionalSummary = summary;
                changed = true;
            }
        }

        return changed;
    }

    private static string ExtractNameFromResume(string resumeText, string resumePath)
    {
        var firstLine = resumeText.Split(['|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => Regex.IsMatch(line, @"^[A-ZÁÉÍÓÚÂÊÔÃÕÇ][a-záéíóúâêôãõç]+(\s+[A-ZÁÉÍÓÚÂÊÔÃÕÇ][a-záéíóúâêôãõç]+){1,3}$"));
        if (!string.IsNullOrWhiteSpace(firstLine)) return firstLine;

        var fileName = Path.GetFileNameWithoutExtension(resumePath ?? "");
        var cleaned = Regex.Replace(fileName, @"(?i)\bcurriculo\b|\bcv\b|[_-]+", " ").Trim();
        return Regex.IsMatch(cleaned, @"^\S+\s+\S+") ? cleaned : "";
    }

    private static string NormalizeUrl(string value)
    {
        var url = value.Trim().TrimEnd('.', ',', ';', ')', ']');
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://{url}";
    }

    private static (string City, string State) ExtractLocation(string resumeText)
    {
        var cityState = Regex.Match(resumeText, @"\b([A-ZÁÉÍÓÚÂÊÔÃÕÇ][a-záéíóúâêôãõç]+(?:\s+[A-ZÁÉÍÓÚÂÊÔÃÕÇ][a-záéíóúâêôãõç]+){0,3})\s*[-,\/]\s*(SP|RJ|MG|PR|SC|RS|BA|GO|PE|CE|ES|DF|MT|MS|PA|PB|RN|AL|SE|PI|MA|RO|RR|TO|AC|AM|AP)\b");
        if (cityState.Success) return (cityState.Groups[1].Value.Trim(), cityState.Groups[2].Value.ToUpperInvariant());
        return ("", "");
    }

    private static string[] ExtractSkills(string resumeText)
    {
        var known = new[]
        {
            "C#", "ASP.NET Core", ".NET", "Entity Framework", "SQL", "React", "TypeScript", "JavaScript",
            "HTML", "CSS", "PostgreSQL", "MySQL", "SQL Server", "Git", "Docker", "Azure", "API REST"
        };

        return known
            .Where(skill => resumeText.Contains(skill, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
    }

    private static string ExtractProfessionalSummary(string resumeText)
    {
        if (string.IsNullOrWhiteSpace(resumeText)) return "";

        var match = Regex.Match(
            resumeText,
            @"(?i)(resumo|perfil profissional|objetivo)\s*[:\-]?\s*(.+?)(experi[eê]ncia|forma[cç][aã]o|educa[cç][aã]o|habilidades|compet[eê]ncias|projetos|$)");
        var text = match.Success ? match.Groups[2].Value : resumeText;
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length < 80)
        {
            text = Regex.Replace(resumeText, @"\s+", " ").Trim();
        }
        if (text.Length > 700) text = text[..700].Trim();
        return text;
    }

    private static string GetFieldValue(CandidateProfile profile, string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "fullname" => profile.FullName,
            "cpf" => profile.Cpf,
            "email" => profile.Email,
            "phone" => profile.Phone,
            "city" => profile.City,
            "state" => profile.State,
            "country" => profile.Country,
            "linkedinurl" => profile.LinkedinUrl,
            "githuburl" => profile.GithubUrl,
            "portfoliourl" => profile.PortfolioUrl,
            "professionalsummary" => profile.ProfessionalSummary,
            "targetrole" => profile.TargetRole,
            "seniority" => profile.Seniority,
            "salaryexpectation" => profile.SalaryExpectation,
            "resumepath" => profile.ResumePath,
            "skills" => string.Join(", ", profile.Skills),
            _ => ""
        };
    }

    private static bool IsValid(string value, ProfileValidationRule rule)
    {
        if (rule.FieldName.Equals("FullName", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(value) && value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length >= 2;
        }

        if (rule.ValidationType.Equals("not_empty", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        if (rule.ValidationType.Equals("regex", StringComparison.OrdinalIgnoreCase))
        {
            if (rule.FieldName.Equals("Cpf", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(value)) return true;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (string.IsNullOrWhiteSpace(rule.ValidationValue)) return true;
            return Regex.IsMatch(value, rule.ValidationValue);
        }

        if (rule.ValidationType.Equals("length", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(rule.ValidationValue, out var minLen)) return true;
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length >= minLen;
        }

        if (rule.ValidationType.Equals("file_exists", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(value) && File.Exists(value);
        }

        if (rule.ValidationType.Equals("date_range", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return true;
    }

    private static List<ProfileValidationRule> GetDefaultRules(Guid userId)
    {
        return
        [
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Phone", ValidationType = "regex", ValidationValue = @"^(\(?\d{2}\)?\s?\d{4,5}-?\d{4})$", ErrorMessage = "Telefone invalido. Use formato (11) 99999-9999.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Email", ValidationType = "regex", ValidationValue = @"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email invalido.", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "SalaryExpectation", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Pretensao salarial ainda nao informada.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "Skills", ValidationType = "length", ValidationValue = "3", ErrorMessage = "Adicione pelo menos 3 skills ao seu perfil.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "FullName", ValidationType = "regex", ValidationValue = @"^\S+\s+\S+$", ErrorMessage = "Informe nome completo (nome e sobrenome).", IsCritical = true },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "City", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Cidade nao informada.", IsCritical = false },
            new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId, FieldName = "ProfessionalSummary", ValidationType = "not_empty", ValidationValue = "", ErrorMessage = "Resumo profissional vazio. Ajuda a explicar seu valor para a vaga.", IsCritical = false }
        ];
    }
}
