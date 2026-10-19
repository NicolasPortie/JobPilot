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
}
