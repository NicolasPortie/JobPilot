using System.Text.Json;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Services;

public static class AiChatResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static T Parse<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```"))
            cleaned = cleaned.Replace("```json", "", StringComparison.OrdinalIgnoreCase).Replace("```", "").Trim();

        try
        {
            return JsonSerializer.Deserialize<T>(cleaned, JsonOptions) ?? fallback;
        }
        catch
        {
            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start < 0 || end <= start) return fallback;

            try
            {
                return JsonSerializer.Deserialize<T>(cleaned[start..(end + 1)], JsonOptions) ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }

    public static PageStateResponse LocalPageState(PageStateRequest request)
    {
        var text = request.VisibleText.ToLowerInvariant();
        var url = request.Url.ToLowerInvariant();
        if (url.Contains("checkpoint/challenge") || text.Contains("captcha") || text.Contains("verify your identity")) return new("captcha", "Security challenge detected.", 0.9m);
        if (url.Contains("/login") || (text.Contains("email") && text.Contains("password"))) return new("login", "Login form detected.", 0.8m);
        if (text.Contains("candidatura simplificada") || text.Contains("easy apply")) return new("job_page", "LinkedIn application controls are visible.", 0.7m);
        if (text.Contains("submit") || text.Contains("enviar") || text.Contains("avancar") || text.Contains("next")) return new("form", "Application form controls are visible.", 0.6m);
        return new("unknown", "No clear application state detected.", 0.4m);
    }
}
