using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public static class FormFiller
{
    public static string ResolveValue(string label, CandidateProfile? profile, List<ReusableAnswer> answers)
    {
        if (profile is null) return "";
        var normalized = label.ToLowerInvariant();

        var answer = answers.FirstOrDefault(x =>
            normalized.Contains(x.QuestionKey.ToLowerInvariant()) ||
            normalized.Contains(x.QuestionText.ToLowerInvariant()));
        if (answer is not null) return answer.AnswerValue;

        if (normalized.Contains("email")) return profile.Email;
        if (normalized.Contains("phone") || normalized.Contains("telefone") || normalized.Contains("celular")) return profile.Phone;
        if (AgentText.ContainsAny(normalized, ["cpf"]) || Regex.IsMatch(normalized, @"\b0{3}\.0{3}\.0{3}-0{2}\b|\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")) return profile.Cpf;
        if (AgentText.ContainsAny(normalized, ["rg", "documento", "document number", "tax id", "ssn"])) return "";
        if (normalized.Contains("last name") || normalized.Contains("sobrenome")) return SemanticProfileMapper.ExtractLastName(profile.FullName);
        if (normalized.Contains("full name") || normalized.Contains("nome completo")) return profile.FullName;
        if (normalized.Contains("first name") || normalized.Contains("primeiro nome") || normalized.Contains("nome")) return SemanticProfileMapper.ExtractFirstName(profile.FullName);
        if (normalized.Contains("city") || normalized.Contains("cidade")) return profile.City;
        if (normalized.Contains("linkedin")) return profile.LinkedinUrl;
        if (normalized.Contains("github")) return profile.GithubUrl;
        if (normalized.Contains("portfolio") || normalized.Contains("website")) return profile.PortfolioUrl;
        if (normalized.Contains("salary") || normalized.Contains("pretens") || normalized.Contains("clt")) return NormalizeSalary(profile.SalaryExpectation, label);

        return "";
    }

    public static string NormalizeSalary(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var cleaned = value.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        cleaned = cleaned.Replace(".", "").Replace(",", ".");
        return AgentText.ContainsAny(label, ["decimal", "número", "numero", "mais de 0.0"]) ? cleaned : value;
    }

    public static async Task<string> LabelForAsync(IPage page, ILocator input)
    {
        var aria = await input.GetAttributeAsync("aria-label");
        if (!string.IsNullOrWhiteSpace(aria)) return aria.Trim();

        var id = await input.GetAttributeAsync("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            var label = page.Locator($"label[for='{id}']");
            if (await label.CountAsync() > 0) return (await label.First.InnerTextAsync()).Trim();
        }

        var placeholder = await input.GetAttributeAsync("placeholder");
        return placeholder?.Trim() ?? "";
    }
}
