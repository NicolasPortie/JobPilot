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

    public async Task<string> BuildSearchUrlAsync()
    {
        var filter = await _client.GetJobFilterAsync();
        var query = Uri.EscapeDataString(_options.Keywords);
        var location = Uri.EscapeDataString(_options.Location);
        var url = $"https://www.linkedin.com/jobs/search/?keywords={query}&location={location}&f_TPR=r604800";

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
