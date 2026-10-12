using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInDiscoveryService
{
    private readonly AgentOptions _options;
    private readonly JobPilotApiClient _client;

    public LinkedInDiscoveryService(AgentOptions options, JobPilotApiClient client)
    {
        _options = options;
        _client = client;
    }

    // geoIds estáveis do LinkedIn para localidades comuns. O parâmetro textual `location` sozinho
    // costuma ser ignorado pelo LinkedIn (cai no padrão dos EUA); o geoId é o que de fato filtra.
    // Mapa curado e pequeno de propósito (trade-off: cobrir os casos do usuário sem manter uma
    // tabela gigante). Localidades não mapeadas ainda enviam o texto e funcionam melhor autenticado.
    private static readonly Dictionary<string, string> GeoIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["brasil"] = "106057199",
        ["brazil"] = "106057199",
        ["br"] = "106057199",
        ["são paulo"] = "105871508",
        ["sao paulo"] = "105871508",
        ["são paulo, brasil"] = "105871508",
        ["sao paulo, brazil"] = "105871508",
        ["rio de janeiro"] = "105989723",
        ["belo horizonte"] = "105818291",
        ["curitiba"] = "104746682",
        ["porto alegre"] = "104215772",
        ["florianópolis"] = "105061994",
        ["florianopolis"] = "105061994",
        ["brasília"] = "105408110",
        ["brasilia"] = "105408110",
        ["portugal"] = "100364837",
        ["estados unidos"] = "103644278",
        ["united states"] = "103644278",
    };

    internal static string? ResolveGeoId(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var normalized = AgentText.RemoveDiacritics(location.Trim()).ToLowerInvariant();
        foreach (var (key, geoId) in GeoIds)
        {
            if (string.Equals(AgentText.RemoveDiacritics(key).ToLowerInvariant(), normalized, StringComparison.Ordinal))
                return geoId;
        }
        return null;
    }

    public async Task<string> BuildSearchUrlAsync()
    {
        var filter = await _client.GetJobFilterAsync();
        var query = Uri.EscapeDataString(_options.Keywords);
        var location = Uri.EscapeDataString(_options.Location);
        var url = $"https://www.linkedin.com/jobs/search/?keywords={query}&location={location}&f_TPR=r604800";

        var geoId = ResolveGeoId(_options.Location);
        if (geoId is not null)
            url += $"&geoId={geoId}";

        var experienceLevels = new List<string>();
        if (filter?.AllowedSeniorities.Contains("entry") == true || filter?.AllowedSeniorities.Contains("associate") == true)
            experienceLevels.Add("2");
        if (filter?.AllowedSeniorities.Contains("mid") == true || filter?.AllowedSeniorities.Contains("pleno") == true)
            experienceLevels.Add("3");
        if (filter?.AllowedSeniorities.Contains("senior") == true || filter?.AllowedSeniorities.Contains("sênior") == true)
            experienceLevels.Add("4");
        if (filter?.AllowedSeniorities.Contains("lead") == true || filter?.AllowedSeniorities.Contains("manager") == true)
            experienceLevels.Add("5");
        if (filter?.AllowedSeniorities.Contains("director") == true)
            experienceLevels.Add("6");

        var hasJuniorKeyword = !string.IsNullOrWhiteSpace(_options.Keywords) &&
            _options.Keywords.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Any(keyword => keyword.Trim().Equals("junior", StringComparison.OrdinalIgnoreCase) ||
                                keyword.Trim().Equals("jr", StringComparison.OrdinalIgnoreCase));

        if (experienceLevels.Count > 0 && experienceLevels.Count < 5 && !hasJuniorKeyword)
            url += $"&f_E={string.Join("%2C", experienceLevels)}";

        var workplaceTypes = new List<string>();
        if (filter?.AllowedWorkplaceTypes.Contains("onsite") == true || filter?.AllowedWorkplaceTypes.Contains("presencial") == true)
            workplaceTypes.Add("1");
        if (filter?.AllowedWorkplaceTypes.Contains("remote") == true || filter?.AllowedWorkplaceTypes.Contains("remoto") == true)
            workplaceTypes.Add("2");
        if (filter?.AllowedWorkplaceTypes.Contains("hybrid") == true || filter?.AllowedWorkplaceTypes.Contains("hibrido") == true)
            workplaceTypes.Add("3");

        if (workplaceTypes.Count > 0 && workplaceTypes.Count < 3)
            url += $"&f_WT={string.Join("%2C", workplaceTypes)}";

        if (filter?.AllowedApplyTypes.Contains("external_form") != true)
            url += "&f_AL=true";

        return url;
    }
}
