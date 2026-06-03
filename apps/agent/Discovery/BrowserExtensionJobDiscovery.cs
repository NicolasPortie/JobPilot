using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Discovery;

public sealed class BrowserExtensionJobDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IBrowserExtensionTransport _transport;

    public BrowserExtensionJobDiscovery(IBrowserExtensionTransport transport) => _transport = transport;

    public async Task<IReadOnlyList<DiscoveredJobDto>> DiscoverAsync(string searchUrl, int limit, CancellationToken cancellationToken = default)
    {
        await _transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: searchUrl, ReasonCode: "discover.navigate"), cancellationToken);
        await Task.Delay(2500, cancellationToken);

        var jobs = new List<DiscoveredJobDto>();
        var scrolls = 0;
        var maxScrolls = Math.Min(20, Math.Max(4, limit / 6));

        while (jobs.Count < limit && scrolls < maxScrolls)
        {
            var batch = await ExtractAsync(cancellationToken);
            var observation = await _transport.ObserveAsync(cancellationToken);
            batch = EnrichCompanies(batch, observation.VisibleText);
            foreach (var job in batch)
            {
                if (jobs.Any(existing => string.Equals(existing.Url, job.Url, StringComparison.OrdinalIgnoreCase)))
                    continue;

                jobs.Add(job);
                if (jobs.Count >= limit)
                    break;
            }

            await _transport.DispatchAsync(new BrowserExtensionCommand("scroll", ReasonCode: "discover.scroll"), cancellationToken);
            await Task.Delay(1800, cancellationToken);
            scrolls++;
        }

        return jobs;
    }

    private async Task<List<DiscoveredJobDto>> ExtractAsync(CancellationToken cancellationToken)
    {
        var result = await _transport.ExecuteAsync(new BrowserExtensionCommand("extract_jobs", ReasonCode: "discover.extract"), cancellationToken);
        using var document = JsonDocument.Parse(result);
        if (!document.RootElement.TryGetProperty("jobs", out var jobsElement) || jobsElement.ValueKind != JsonValueKind.Array)
            return [];

        var jobs = new List<DiscoveredJobDto>();
        foreach (var item in jobsElement.EnumerateArray())
        {
            var title = ReadString(item, "title");
            var url = ReadString(item, "url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                continue;

            jobs.Add(new DiscoveredJobDto(
                title,
                ReadString(item, "company"),
                ReadString(item, "location"),
                ReadString(item, "description"),
                url,
                item.TryGetProperty("isRemote", out var isRemote) && isRemote.ValueKind == JsonValueKind.True));
        }

        return jobs;
    }

    private static string ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static List<DiscoveredJobDto> EnrichCompanies(List<DiscoveredJobDto> jobs, string visibleText) =>
        jobs.Select(job =>
        {
            if (!CompanyLooksLikeTitle(job.Company, job.Title))
                return job;

            var company = InferCompanyFromVisibleText(job.Title, visibleText);
            return string.IsNullOrWhiteSpace(company)
                ? job with { Company = "" }
                : job with { Company = company };
        }).ToList();

    private static bool CompanyLooksLikeTitle(string company, string title)
    {
        if (string.IsNullOrWhiteSpace(company))
            return true;

        var normalizedCompany = Normalize(company);
        var normalizedTitle = Normalize(title);
        return normalizedCompany == normalizedTitle ||
               normalizedCompany.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains(normalizedCompany, StringComparison.OrdinalIgnoreCase);
    }

    private static string InferCompanyFromVisibleText(string title, string visibleText)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(visibleText))
            return "";

        var compact = Regex.Replace(visibleText, @"\s+", " ");
        var escapedTitle = Regex.Escape(title);
        var match = Regex.Match(compact, $"{escapedTitle}\\s+{escapedTitle}\\s+(?:with verification\\s+)?", RegexOptions.IgnoreCase);
        if (!match.Success)
            return "";

        var tail = compact[(match.Index + match.Length)..].Trim();
        var marker = Regex.Match(tail,
            @"\s+(São Paulo|Sao Paulo|Rio de Janeiro|Belo Horizonte|Brasil|Brazil|Curitiba|Campinas|Florianópolis|Florianopolis|Porto Alegre|[A-ZÁ-Ú][\wÀ-ÿ.'+-]+(?:\s+e\s+Regi(?:ão|ao))|\(Remoto\)|Remoto)\b",
            RegexOptions.IgnoreCase);
        if (!marker.Success || marker.Index <= 0)
            return "";

        var company = tail[..marker.Index].Trim();
        company = Regex.Replace(company, @"\s+with verification$", "", RegexOptions.IgnoreCase).Trim();
        return CompanyLooksLikeTitle(company, title) ? "" : company;
    }

    private static string Normalize(string value) =>
        Regex.Replace(AgentText.RemoveDiacritics(value).ToLowerInvariant(), @"\s+", " ").Trim();
}
