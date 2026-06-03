using JobPilot.Agent.Application;
using JobPilot.Agent.Infrastructure;
using JobPilot.Agent.Platforms.LinkedIn;
using Microsoft.Playwright;

namespace JobPilot.Agent.Discovery;

public sealed class LinkedInNavigator
{
    private readonly LinkedInBrowserContextLauncher _browserContext;
    private readonly LinkedInDiscoveryService _discovery;
    private readonly LinkedInEasyApplySurface _easyApply;
    private readonly LinkedInLoginService _login;

    public LinkedInNavigator(AgentOptions options, JobPilotApiClient client)
    {
        _browserContext = new LinkedInBrowserContextLauncher(options);
        _discovery = new LinkedInDiscoveryService(options, client);
        _easyApply = new LinkedInEasyApplySurface();
        _login = new LinkedInLoginService(options);
    }

    public Task<IBrowserContext> LaunchContextAsync(IPlaywright playwright) =>
        _browserContext.LaunchAsync(playwright);

    public Task<string> BuildSearchUrlAsync() =>
        _discovery.BuildSearchUrlAsync();

    public Task<bool> ClickEasyApplyAsync(IPage page) =>
        _easyApply.ClickEasyApplyAsync(page);

    public Task<ApplicationSurfaceOpenResult> OpenApplicationSurfaceAsync(IPage page) =>
        _easyApply.OpenAsync(page);

    public Task<bool> NeedsLoginAsync(IPage page) =>
        _login.NeedsLoginAsync(page);

    public Task<bool> TryLoginAsync(IPage page, string? email = null, string? password = null) =>
        _login.TryLoginAsync(page, email, password);
}
