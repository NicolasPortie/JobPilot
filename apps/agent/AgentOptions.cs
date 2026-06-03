namespace JobPilot.Agent;

public sealed record AgentOptions(
    string Mode,
    string Token,
    string ApiBaseUrl,
    string Keywords,
    string Location,
    int Limit,
    bool AutoSubmit,
    string PolicyMode,
    int MaxApplications,
    int LinkedInLoginWaitSeconds,
    string BrowserDriverMode,
    string BrowserChannel,
    string BrowserPath,
    string BrowserProfilePath,
    string LinkedInEmail,
    string LinkedInPassword,
    bool Headless,
    bool Continuous,
    int DiscoveryIntervalMinutes,
    string? TaskId,
    bool DryRun,
    bool ShowHelp)
{
    public static AgentOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
            return new("", "", "", "", "", 0, false, "conservative", 0, 20, "playwright", "chrome", "", ResolveDefaultBrowserProfilePath(), "", "", false, false, 10, null, false, true);

        string Get(string key, string fallback = "")
        {
            var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        bool Has(string key) => args.Contains(key, StringComparer.OrdinalIgnoreCase);

        bool GetBool(string key, string envName, bool fallback = false)
        {
            var index = Array.FindIndex(args, arg => string.Equals(arg, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    return true;

                return bool.TryParse(args[index + 1], out var parsed) ? parsed : true;
            }

            var envValue = Env(envName);
            return string.IsNullOrWhiteSpace(envValue)
                ? fallback
                : bool.TryParse(envValue, out var parsedEnv) && parsedEnv;
        }

        return new AgentOptions(
            args[0],
            Get("--token", Env("JOBPILOT_AGENT_TOKEN")),
            Get("--api", Env("JOBPILOT_API_URL", "http://localhost:5000")),
            Get("--keywords", ".NET"),
            Get("--location", "Brazil"),
            int.TryParse(Get("--limit"), out var limit) ? limit : 25,
            GetBool("--auto-submit", "JOBPILOT_AUTO_SUBMIT"),
            Get("--policy", Env("JOBPILOT_AUTOPILOT_POLICY", "conservative")),
            int.TryParse(Get("--max-applications"), out var max) ? max : 10,
            int.TryParse(Env("LINKEDIN_LOGIN_WAIT_SECONDS"), out var wait) ? wait : 20,
            Get("--browser-driver", Env("JOBPILOT_BROWSER_DRIVER", "playwright")).Trim().ToLowerInvariant(),
            Env("JOBPILOT_BROWSER_CHANNEL", "chrome"),
            Env("JOBPILOT_BROWSER_PATH", ""),
            Get("--browser-profile", Env("JOBPILOT_BROWSER_PROFILE", ResolveDefaultBrowserProfilePath())),
            Env("LINKEDIN_EMAIL", ""),
            Env("LINKEDIN_PASSWORD", ""),
            GetBool("--headless", "JOBPILOT_HEADLESS"),
            Has("--continuous"),
            int.TryParse(Get("--interval-minutes"), out var interval) ? interval : 10,
            Get("--task"),
            GetBool("--dry-run", "JOBPILOT_DRY_RUN"),
            false);
    }

    private static string Env(string name, string fallback = "")
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ResolveDefaultBrowserProfilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;

        return Path.Combine(root, "JobPilot", "browser-profile");
    }
}
