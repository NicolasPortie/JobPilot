using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.Inhire;

public sealed class InhireApplicationFlow : IApplicationPlatformFlow
{
    public bool CanHandle(JobLead lead) =>
        lead.ApplyPlatform.Equals("inhire", StringComparison.OrdinalIgnoreCase) ||
        lead.ApplyUrl.Contains("inhire.app", StringComparison.OrdinalIgnoreCase) ||
        lead.Url.Contains("inhire.app", StringComparison.OrdinalIgnoreCase);

    public async Task NavigateToLeadAsync(IPage page, AgentTaskResponse task)
    {
        var targetUrl = !string.IsNullOrWhiteSpace(task.Lead.ApplyUrl) ? task.Lead.ApplyUrl : task.Lead.Url;
        if (!page.Url.Contains("inhire.app", StringComparison.OrdinalIgnoreCase) ||
            !page.Url.Equals(targetUrl, StringComparison.OrdinalIgnoreCase))
        {
            await page.GotoAsync(targetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        }

        await page.WaitForTimeoutAsync(2500);
    }

    public Task RecoverAfterLoginAsync(IPage page, AgentTaskResponse task) =>
        Task.CompletedTask;

    public Task<ApplicationSurfaceOpenResult> OpenApplicationSurfaceAsync(IPage page, AgentTaskResponse task) =>
        Task.FromResult(ApplicationSurfaceOpenResult.OpenedSurface(
            "inhire",
            "InHire",
            page.Url,
            "InHire application page is active.",
            page));
}
