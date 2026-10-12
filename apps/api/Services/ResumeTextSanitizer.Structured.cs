using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static partial class ResumeTextSanitizer
{
    public static JsonElement? SectionObject(IEnumerable<ResumeSection> sections, string type)
    {
        var section = sections.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.SectionType.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (section is null || string.IsNullOrWhiteSpace(section.ContentJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(section.ContentJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    public static List<Dictionary<string, string>> SectionItems(IEnumerable<ResumeSection> sections, string type)
    {
        var root = SectionObject(sections, type);
        if (root is null) return [];
        var element = root.Value;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items.EnumerateArray().Select(JsonObjectToDictionary).Where(x => x.Count > 0).ToList();
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(JsonObjectToDictionary).Where(x => x.Count > 0).ToList();
        return [JsonObjectToDictionary(element)];
    }

    public static Dictionary<string, string> JsonObjectToDictionary(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return new Dictionary<string, string> { ["description"] = element.GetString() ?? "" };
        if (element.ValueKind != JsonValueKind.Object) return [];
        return element.EnumerateObject()
            .ToDictionary(property => property.Name, property => JsonElementToText(property.Value), StringComparer.OrdinalIgnoreCase);
    }

    public static string[] SectionArray(IEnumerable<ResumeSection> sections, string type, string propertyName)
    {
        var root = SectionObject(sections, type);
        if (root is null) return [];
        if (root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Array)
                return property.EnumerateArray().Select(JsonElementToText).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (property.ValueKind == JsonValueKind.String)
                return [property.GetString() ?? ""];
        }
        return [];
    }

    public static string SectionText(IEnumerable<ResumeSection> sections, string type)
    {
        var root = SectionObject(sections, type);
        if (root is null) return "";
        if (root.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "text", "summary", "description" })
                if (root.Value.TryGetProperty(key, out var property))
                    return JsonElementToText(property);
        }
        return JsonElementToText(root.Value);
    }

    public static string JsonValue(JsonElement? element, string key)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return "";
        return element.Value.TryGetProperty(key, out var property) ? JsonElementToText(property) : "";
    }

    public static string JsonElementToText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(JsonElementToText).Where(x => !string.IsNullOrWhiteSpace(x))),
            JsonValueKind.Object => string.Join(", ", element.EnumerateObject().Select(property => JsonElementToText(property.Value)).Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => ""
        };
    }

    public static string Value(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : "";

    public static string LimitText(string value, int maxLength)
    {
        var cleaned = NormalizeForResumeSection(value);
        if (cleaned.Length <= maxLength) return cleaned;

        var candidate = cleaned[..maxLength].Trim();
        var sentenceEnd = Math.Max(candidate.LastIndexOf('.'), Math.Max(candidate.LastIndexOf('!'), candidate.LastIndexOf('?')));
        if (sentenceEnd > maxLength * 0.55)
            return candidate[..(sentenceEnd + 1)].Trim();

        var lineEnd = candidate.LastIndexOf('\n');
        if (lineEnd > maxLength * 0.55)
            return candidate[..lineEnd].Trim();

        var space = candidate.LastIndexOf(' ');
        return space > maxLength * 0.55 ? candidate[..space].Trim() : candidate;
    }

    public static string NormalizeForResumeSection(string value) =>
        string.Join("\n", (value ?? "")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]{2,}", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)))
            .Trim();

    public static string LayoutLabel(string layout) =>
        ResumeTailoringService.LayoutOptions.FirstOrDefault(x => x.Key == NormalizeLayout(layout))?.Label ?? "ATS compacto";

    public static string CleanEvidence(string value)
    {
        var cleaned = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return cleaned.Length <= 240 ? cleaned : $"{cleaned[..240].Trim()}...";
    }

    public static string CleanBaseText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var cleaned = value.Trim();
        return cleaned.Length <= 1800 ? cleaned : $"{cleaned[..1800].Trim()}...";
    }

    public static string[] MergeSkills(string[] profileSkills, string[] provenKeywords) =>
        NormalizeSkillList(profileSkills.Concat(provenKeywords));

    public static string[] NormalizeSkillList(IEnumerable<string> values) =>
        values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(CleanSkill)
            .Where(IsValidSkill)
            .GroupBy(SkillKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(SkillDisplayPriority).ThenBy(x => x.Length).First())
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool LooksLikeSkillListSentence(string sentence, string[] skills)
    {
        if (string.IsNullOrWhiteSpace(sentence) || skills.Length == 0) return false;
        var matches = skills.Count(skill => ContainsTerm(sentence, skill));
        return matches >= 3 || Regex.IsMatch(sentence, @"(?i)\b(com uso de|stack|tecnologias?:|compet[eê]ncias?:|skills?:)\b");
    }

    public static bool IsMalformedResumeLine(string value)
    {
        var text = Regex.Replace(value ?? "", @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(text)) return true;
        return Regex.IsMatch(text, @"(?i)\b(com|em|de|para|por|utilizando|usando)\s*[.:;]?$");
    }

    private static string SkillKey(string value)
    {
        var normalized = RemoveDiacritics(CleanSkill(value)).ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.]+", " ").Trim();
        return normalized switch
        {
            "api rest" or "apis rest" or "rest api" or "rest apis" or "rest" => "api-rest",
            "asp net" or "asp net core" or "asp.net" or "asp.net core" => "aspnet-core",
            "entity framework" or "entity framework core" or "ef core" => "entity-framework",
            "postgres" or "postgresql" => "postgresql",
            "dotnet" or ".net" or "net" => ".net",
            "c sharp" or "csharp" or "c#" => "c#",
            _ => normalized
        };
    }

    private static int SkillDisplayPriority(string value)
    {
        var key = SkillKey(value);
        if (key == "api-rest" && value.Equals("API REST", StringComparison.Ordinal)) return 10;
        if (key == "aspnet-core" && value.Equals("ASP.NET Core", StringComparison.Ordinal)) return 10;
        if (key == "entity-framework" && value.Equals("Entity Framework", StringComparison.Ordinal)) return 10;
        if (key == "postgresql" && value.Equals("PostgreSQL", StringComparison.Ordinal)) return 10;
        return value.Any(char.IsLower) && value.Any(char.IsUpper) ? 5 : 0;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    public static string NormalizeLayout(string value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace("-", "_");
        normalized = normalized switch
        {
            "ats_reverse_chronological" or "ats_classic" or "ats_skills_first" or "ats_modern_minimal" => "ats_standard",
            _ => normalized
        };
        return ResumeTailoringService.LayoutOptions.Any(x => x.Key == normalized) ? normalized : "ats_compact";
    }

    public static void Section(StringBuilder sb, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        Append(sb, title);
        foreach (var line in content.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
            Append(sb, line);
        Blank(sb);
    }

    public static void Append(StringBuilder sb, string? value, string fallback = "")
    {
        var text = FirstNonEmpty(value, fallback);
        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
    }

    public static void Blank(StringBuilder sb) => sb.AppendLine();

    public static string BuildContactLine(CandidateProfile? profile, JsonElement? identity, string sourceText = "")
    {
        var values = new List<string>();
        AddUnique(values, profile?.Email);
        AddUnique(values, JsonValue(identity, "email"));
        AddUnique(values, JsonValue(identity, "Email"));
        AddUnique(values, profile?.Phone, NormalizePhone);
        AddUnique(values, JsonValue(identity, "phone"), NormalizePhone);
        AddUnique(values, JsonValue(identity, "Phone"), NormalizePhone);
        AddUnique(values, BuildLocationLine(profile, identity, sourceText));
        return string.Join(" | ", values);
    }

    public static string BuildLocationLine(CandidateProfile? profile, JsonElement? identity, string sourceText = "")
    {
        var city = FirstNonEmpty(profile?.City, JsonValue(identity, "city"), JsonValue(identity, "City"));
        var country = NormalizeCountry(FirstNonEmpty(profile?.Country, JsonValue(identity, "country"), JsonValue(identity, "Country")));
        var sourceLocation = ExtractBrazilianLocation(sourceText, city);
        var state = FirstNonEmpty(profile?.State, JsonValue(identity, "state"), JsonValue(identity, "State"), sourceLocation.State);
        var displayCity = FirstNonEmpty(sourceLocation.City, city);

        if (!string.IsNullOrWhiteSpace(displayCity) && !string.IsNullOrWhiteSpace(state))
            return $"{NormalizeCity(displayCity)}/{state}";

        if (!string.IsNullOrWhiteSpace(displayCity))
            return NormalizeCity(displayCity);

        return IsBrazil(country) ? "" : country;
    }

    public static void AddUnique(List<string> values, string? value, Func<string, string>? keySelector = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var cleaned = value.Trim();
        var key = keySelector?.Invoke(cleaned) ?? cleaned;
        if (string.IsNullOrWhiteSpace(key)) return;
        var duplicateIndex = values.FindIndex(existing => string.Equals(keySelector?.Invoke(existing) ?? existing, key, StringComparison.OrdinalIgnoreCase));
        if (duplicateIndex >= 0)
        {
            if (keySelector is not null && cleaned.Count(char.IsPunctuation) > values[duplicateIndex].Count(char.IsPunctuation))
                values[duplicateIndex] = cleaned;
            return;
        }
        values.Add(cleaned);
    }

    public static string NormalizePhone(string value)
    {
        var digits = Regex.Replace(value ?? "", @"\D", "");
        if (digits.Length > 11 && digits.StartsWith("55", StringComparison.Ordinal)) digits = digits[2..];
        return digits;
    }

    private static (string City, string State) ExtractBrazilianLocation(string sourceText, string city)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(city)) return ("", "");
        var matches = Regex.Matches(sourceText, @"\b([A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}'-]+(?:\s+[A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}'-]+){0,3})\s*[-,\/]\s*(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)\b", RegexOptions.IgnoreCase);
        var normalizedCity = StripDiacritics(city).ToLowerInvariant();
        foreach (Match match in matches)
        {
            var candidate = StripDiacritics(match.Groups[1].Value).ToLowerInvariant();
            if (candidate == normalizedCity)
                return (match.Groups[1].Value.Trim(), match.Groups[2].Value.ToUpperInvariant());
        }
        return ("", "");
    }

    private static string NormalizeCity(string city) =>
        Regex.Replace(city ?? "", @"\s+", " ").Trim();

    private static string NormalizeCountry(string country) =>
        IsBrazil(country) ? "Brasil" : Regex.Replace(country ?? "", @"\s+", " ").Trim();

    private static bool IsBrazil(string country)
    {
        var normalized = StripDiacritics(country ?? "").Trim().ToLowerInvariant();
        return normalized is "brazil" or "brasil" or "br";
    }

    private static string StripDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string JoinUniqueNonEmpty(string separator, params string?[] values) =>
        string.Join(separator, values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public static string JoinNonEmpty(string separator, params string?[] values) =>
        string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

    public static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";

    public static bool ContainsAny(string value, string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    public static bool ContainsTerm(string value, string term)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(term)) return false;
        return value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
