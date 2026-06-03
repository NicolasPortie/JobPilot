using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInBrowserContextLauncher
{
    private readonly AgentOptions _options;

    public LinkedInBrowserContextLauncher(AgentOptions options) => _options = options;

    public async Task<IBrowserContext> LaunchAsync(IPlaywright playwright)
    {
        var profile = string.IsNullOrWhiteSpace(_options.BrowserProfilePath)
            ? Path.Combine(AppContext.BaseDirectory, ".jobpilot-profile")
            : _options.BrowserProfilePath;
        Directory.CreateDirectory(profile);
        var launchOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.Headless ? 0 : 80,
            ViewportSize = new ViewportSize { Width = 1366, Height = 850 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            Locale = "pt-BR",
            TimezoneId = "America/Sao_Paulo"
        };

        if (!string.IsNullOrWhiteSpace(_options.BrowserPath) && File.Exists(_options.BrowserPath))
            launchOptions.ExecutablePath = _options.BrowserPath;
        else if (!string.IsNullOrWhiteSpace(_options.BrowserChannel))
            launchOptions.Channel = _options.BrowserChannel;

        return await playwright.Chromium.LaunchPersistentContextAsync(profile, launchOptions);
    }
}
