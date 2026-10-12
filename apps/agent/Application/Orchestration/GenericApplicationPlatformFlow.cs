using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public sealed class GenericApplicationPlatformFlow : IApplicationPlatformFlow
{
    public static GenericApplicationPlatformFlow Instance { get; } = new();

    private GenericApplicationPlatformFlow()
    {
    }

    public bool CanHandle(JobLead lead) => true;

    public async Task NavigateToLeadAsync(IPage page, AgentTaskResponse task)
    {
        await page.GotoAsync(task.Lead.Url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(2500);
    }

    public Task RecoverAfterLoginAsync(IPage page, AgentTaskResponse task) =>
        Task.CompletedTask;

    public Task<ApplicationSurfaceOpenResult> OpenApplicationSurfaceAsync(IPage page, AgentTaskResponse task) =>
        Task.FromResult(ApplicationSurfaceOpenResult.OpenedSurface(
            "generic_form",
            "Generic",
            page.Url,
            "Generic application page is already active.",
            page));
}
