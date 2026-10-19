using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobPilot.Api.Services;

public sealed class JobTargetUrlExtractionService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResolvedJobSource> ResolveAsync(string url, string manualText)
    {
        url = (url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("URL da vaga não informada.");

        if (!OutboundUrlPolicy.TryValidatePublicHttpUrl(url, out var safeUri))
            throw new InvalidOperationException("A URL da vaga precisa ser HTTP(S) e apontar para um host público.");
        url = safeUri.ToString();

        if (TryExtractLinkedInJobId(url, out var linkedInJobId))
            return await ResolveLinkedInAsync(url, linkedInJobId, manualText);

        try
        {
            var html = await FetchAsync(url);
            var structured = TryExtractStructuredJobPosting(html, url, manualText);
            if (structured is not null)
                return structured;

            var text = ResumeTextSanitizer.JoinNonEmpty("\n", manualText, HtmlToText(html));
            if (!string.IsNullOrWhiteSpace(text) && text.Length >= 80)
                return new ResolvedJobSource("url", text, url, "", "", "", [], []);
        }
        catch (HttpRequestException ex)
        {
            return BuildUrlFallbackSource(url, manualText, $"Nao foi possivel baixar a URL da vaga ({ex.StatusCode?.ToString() ?? "erro HTTP"}). Cole a descricao completa ou envie screenshot/PDF para melhorar a aderencia.");
        }
        catch (TaskCanceledException)
        {
            return BuildUrlFallbackSource(url, manualText, "Tempo esgotado ao baixar a URL da vaga. Cole a descricao completa ou envie screenshot/PDF para melhorar a aderencia.");
        }

        return BuildUrlFallbackSource(url, manualText, "Nao foi possivel extrair texto suficiente da URL. Cole a descricao completa ou envie screenshot/PDF para melhorar a aderencia.");
    }

    private async Task<ResolvedJobSource> ResolveLinkedInAsync(string originalUrl, string jobId, string manualText)
    {
        var canonicalUrl = $"https://www.linkedin.com/jobs/view/{jobId}/";
        var guestUrl = $"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{jobId}";
        try
        {
            var html = await FetchAsync(guestUrl);
            var title = ExtractClassText(html, "top-card-layout__title");
            var company = ExtractClassText(html, "topcard__org-name-link");
            var location = ExtractTopCardLocation(html);
            var descriptionHtml = ExtractClassHtml(html, "show-more-less-html__markup");
            var description = HtmlToText(descriptionHtml);
            var criteria = ExtractLinkedInCriteria(html);

            var sourceText = ResumeTextSanitizer.JoinNonEmpty("\n\n",
                ResumeTextSanitizer.CleanProfessionalTitle(title),
                string.IsNullOrWhiteSpace(company) ? "" : $"Empresa: {company}",
                string.IsNullOrWhiteSpace(location) ? "" : $"Localizacao: {location}",
                $"URL: {canonicalUrl}",
                $"LinkedIn Job ID: {jobId}",
                string.IsNullOrWhiteSpace(description) ? "" : $"Descricao completa da vaga:\n{description}",
                string.IsNullOrWhiteSpace(criteria) ? "" : $"Criterios da vaga:\n{criteria}",
                string.IsNullOrWhiteSpace(manualText) ? "" : $"Observacoes manuais do usuario:\n{manualText}");

            if (sourceText.Length >= 500 && !string.IsNullOrWhiteSpace(description))
                return new ResolvedJobSource("url", sourceText, canonicalUrl, title, company, location, [], []);

            return BuildUrlFallbackSource(originalUrl, manualText, "LinkedIn retornou a vaga, mas o texto principal veio incompleto. Cole a descricao completa ou envie screenshot/PDF.");
        }
        catch (HttpRequestException ex)
        {
            return BuildUrlFallbackSource(originalUrl, manualText, $"Nao foi possivel baixar a vaga publica do LinkedIn ({ex.StatusCode?.ToString() ?? "erro HTTP"}). Cole a descricao completa ou envie screenshot/PDF para melhorar a aderencia.");
        }
        catch (TaskCanceledException)
        {
            return BuildUrlFallbackSource(originalUrl, manualText, "Tempo esgotado ao baixar a vaga publica do LinkedIn. Cole a descricao completa ou envie screenshot/PDF para melhorar a aderencia.");
        }
    }

    private async Task<string> FetchAsync(string url)
    {
        OutboundUrlPolicy.EnsurePublicHttpUrl(url);
        // Sites como o LinkedIn respondem 429 sob rajada: espera curta e tenta de novo.
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            using var response = await http.SendAsync(request);
            if ((int)response.StatusCode is 429 or 503 && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(1.5 * (attempt + 1)));
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    private static bool TryExtractLinkedInJobId(string url, out string jobId)
    {
        jobId = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, "linkedin.com"))
            return false;

        var parameters = ParseQuery(uri.Query);
        jobId = parameters.TryGetValue("currentJobId", out var currentJobId) ? currentJobId : "";
        if (string.IsNullOrWhiteSpace(jobId))
            jobId = parameters.TryGetValue("referenceJobId", out var referenceJobId) ? referenceJobId : "";
        if (string.IsNullOrWhiteSpace(jobId))
        {
            var match = Regex.Match(uri.AbsolutePath, @"/jobs/view/(?<id>\d+)", RegexOptions.IgnoreCase);
            if (match.Success) jobId = match.Groups["id"].Value;
        }

        jobId = Regex.Match(jobId ?? "", @"\d+").Value;
        return !string.IsNullOrWhiteSpace(jobId);
    }

    private static ResolvedJobSource? TryExtractStructuredJobPosting(string html, string url, string manualText)
    {
        foreach (Match match in Regex.Matches(html ?? "", @"<script[^>]+type\s*=\s*[""']application/ld\+json[""'][^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            try
            {
                using var document = JsonDocument.Parse(WebUtility.HtmlDecode(match.Groups["json"].Value));
                var job = FindJobPosting(document.RootElement);
                if (job.ValueKind == JsonValueKind.Undefined) continue;

                var title = ReadString(job, "title");
                var company = ReadNestedString(job, "hiringOrganization", "name");
                var location = ReadJobLocation(job);
                var description = HtmlToText(ReadString(job, "description"));
                var extra = ResumeTextSanitizer.JoinNonEmpty("\n",
                    ReadString(job, "employmentType"),
                    ReadString(job, "experienceRequirements"),
                    ReadString(job, "educationRequirements"),
                    ReadString(job, "qualifications"),
                    ReadString(job, "responsibilities"),
                    ReadString(job, "skills"));
                var sourceText = ResumeTextSanitizer.JoinNonEmpty("\n\n",
                    ResumeTextSanitizer.CleanProfessionalTitle(title),
                    string.IsNullOrWhiteSpace(company) ? "" : $"Empresa: {company}",
                    string.IsNullOrWhiteSpace(location) ? "" : $"Localizacao: {location}",
                    $"URL: {url}",
                    string.IsNullOrWhiteSpace(description) ? "" : $"Descricao completa da vaga:\n{description}",
                    string.IsNullOrWhiteSpace(extra) ? "" : $"Dados estruturados da vaga:\n{extra}",
                    string.IsNullOrWhiteSpace(manualText) ? "" : $"Observacoes manuais do usuario:\n{manualText}");

                if (sourceText.Length >= 80)
                    return new ResolvedJobSource("url", sourceText, url, title, company, location, [], []);
            }
            catch
            {
                // Ignore invalid JSON-LD blocks and continue with HTML extraction.
            }
        }

        return null;
    }

    private static JsonElement FindJobPosting(JsonElement element)
    {
        if (IsJobPosting(element)) return element;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindJobPosting(item);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
            }
        }
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("@graph", out var graph))
            return FindJobPosting(graph);
        return default;
    }

    private static bool IsJobPosting(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("@type", out var type))
            return false;
        if (type.ValueKind == JsonValueKind.String)
            return type.GetString()?.Equals("JobPosting", StringComparison.OrdinalIgnoreCase) == true;
        if (type.ValueKind == JsonValueKind.Array)
            return type.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString()?.Equals("JobPosting", StringComparison.OrdinalIgnoreCase) == true);
        return false;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return "";
        return ReadStringValue(value);
    }

    private static string ReadNestedString(JsonElement element, string property, string nestedProperty)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return "";
        if (value.ValueKind == JsonValueKind.Array)
            value = value.EnumerateArray().FirstOrDefault();
        return value.ValueKind == JsonValueKind.Object ? ReadString(value, nestedProperty) : "";
    }

    private static string ReadJobLocation(JsonElement job)
    {
        if (job.ValueKind != JsonValueKind.Object || !job.TryGetProperty("jobLocation", out var location))
            return "";
        if (location.ValueKind == JsonValueKind.Array)
            location = location.EnumerateArray().FirstOrDefault();
        if (location.ValueKind != JsonValueKind.Object)
            return "";
        if (!location.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object)
            return ReadStringValue(location);
        return ResumeTextSanitizer.JoinNonEmpty(", ",
            ReadString(address, "addressLocality"),
            ReadString(address, "addressRegion"),
            ReadString(address, "addressCountry"));
    }

    private static string ReadStringValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? "",
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(ReadStringValue).Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => ""
        };

    private static string ExtractClassText(string html, string className) =>
        HtmlToText(ExtractClassHtml(html, className));

    private static string ExtractClassHtml(string html, string className)
    {
        var pattern = $@"<(?<tag>[a-z0-9]+)[^>]*class\s*=\s*[""'][^""']*{Regex.Escape(className)}[^""']*[""'][^>]*>(?<value>.*?)</\k<tag>>";
        var match = Regex.Match(html ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : "";
    }

    private static string ExtractTopCardLocation(string html)
    {
        var matches = Regex.Matches(html ?? "", @"<span[^>]*class\s*=\s*[""'][^""']*topcard__flavor[^""']*topcard__flavor--bullet[^""']*[""'][^>]*>(?<value>.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return matches.Count == 0 ? "" : HtmlToText(matches[0].Groups["value"].Value);
    }

    private static string ExtractLinkedInCriteria(string html)
    {
        var criteria = new List<string>();
        var pattern = @"<li[^>]*class\s*=\s*[""'][^""']*description__job-criteria-item[^""']*[""'][^>]*>.*?<h3[^>]*>(?<label>.*?)</h3>.*?<span[^>]*>(?<value>.*?)</span>.*?</li>";
        foreach (Match match in Regex.Matches(html ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var label = HtmlToText(match.Groups["label"].Value);
            var value = HtmlToText(match.Groups["value"].Value);
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
                criteria.Add($"{label}: {value}");
        }
        return string.Join("\n", criteria);
    }

    private static ResolvedJobSource BuildUrlFallbackSource(string url, string manualText, string warning)
    {
        var urlContext = BuildUrlContext(url);
        var text = ResumeTextSanitizer.JoinNonEmpty("\n", manualText, urlContext);
        if (text.Length < 80)
            text = ResumeTextSanitizer.JoinNonEmpty("\n", text, "Contexto parcial de vaga informado por URL. Requisitos completos nao foram extraidos automaticamente.");
        return new ResolvedJobSource("url", text, url, ExtractTitleFromUrlContext(urlContext), "", "", [], [warning]);
    }

    private static string BuildUrlContext(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return $"URL da vaga informada: {url}";

        var parameters = ParseQuery(uri.Query);
        parameters.TryGetValue("currentJobId", out var currentJobId);
        parameters.TryGetValue("keywords", out var keywords);
        if (string.IsNullOrWhiteSpace(currentJobId))
        {
            var match = Regex.Match(uri.AbsolutePath, @"/jobs/view/(?<id>\d+)", RegexOptions.IgnoreCase);
            if (match.Success) currentJobId = match.Groups["id"].Value;
        }

        return ResumeTextSanitizer.JoinNonEmpty("\n",
            $"URL da vaga informada: {url}",
            string.IsNullOrWhiteSpace(currentJobId) ? "" : $"ID da vaga no LinkedIn: {currentJobId}",
            string.IsNullOrWhiteSpace(keywords) ? "" : $"Termos da busca/cargo informados na URL: {keywords}",
            "Conteudo completo da pagina nao disponivel para o servidor; trate estes dados como contexto parcial e revise requisitos antes de enviar.");
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .GroupBy(parts => DecodeQueryValue(parts[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => DecodeQueryValue(group.Last()[1]), StringComparer.OrdinalIgnoreCase);

    private static string DecodeQueryValue(string value) =>
        Uri.UnescapeDataString((value ?? "").Replace("+", " ")).Trim();

    private static string ExtractTitleFromUrlContext(string urlContext)
    {
        var match = Regex.Match(urlContext, @"(?im)^Termos da busca/cargo informados na URL:\s*(?<title>.+)$");
        return match.Success ? match.Groups["title"].Value.Trim() : "";
    }

    private static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = html;
        text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</\s*(p|div|section|h[1-6]|li|ul|ol)\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*li[^>]*>", "- ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<script.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n\s+", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
