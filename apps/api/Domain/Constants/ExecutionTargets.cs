namespace JobPilot.Api.Domain;

public static class ExecutionTargets
{
    public const string Extension = "extension";
    public const string LocalDedicatedBrowser = "local_dedicated_browser";
    public const string RemoteBrowser = "remote_browser";
    public const string ServerConnector = "server_connector";

    public static string Normalize(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace("-", "_");
        return normalized switch
        {
            "" => Extension,
            "playwright" or "local_browser" or "local_dedicated_browser" => LocalDedicatedBrowser,
            "remote" or "remote_browser" => RemoteBrowser,
            "server" or "server_connector" => ServerConnector,
            _ => Extension
        };
    }

    public static bool IsImplemented(string value) =>
        value is Extension or LocalDedicatedBrowser;
}
