using System.Text.RegularExpressions;
using JobPilot.Agent.Application;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInApplicationFlow : IApplicationPlatformFlow
{
    private readonly LinkedInNavigator _navigator;

    public LinkedInApplicationFlow(LinkedInNavigator navigator)
    {
        _navigator = navigator;
    }

    public bool CanHandle(JobLead lead) =>
        lead.Platform.Equals("linkedin", StringComparison.OrdinalIgnoreCase) ||
        lead.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase);

    public async Task NavigateToLeadAsync(IPage page, AgentTaskResponse task)
    {
        var jobId = ExtractJobId(task.Lead.Url);
        var navigationUrl = jobId is null
            ? task.Lead.Url
            : $"https://www.linkedin.com/jobs/collections/similar-jobs/?currentJobId={jobId}";

        await page.GotoAsync(navigationUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(4000);

        if (jobId is not null)
            await EnsureJobLoadedAsync(page, task, jobId);
    }

    public async Task RecoverAfterLoginAsync(IPage page, AgentTaskResponse task)
    {
        if (page.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase))
            return;

        var jobId = ExtractJobId(task.Lead.Url);
        if (jobId is null)
            return;

        Console.WriteLine($"[RedirectFix] Pos-login em {page.Url}. Navegando para similar-jobs.");
        await page.GotoAsync($"https://www.linkedin.com/jobs/collections/similar-jobs/?currentJobId={jobId}", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(4000);
        await EnsureJobLoadedAsync(page, task, jobId);
    }

    public async Task<ApplicationSurfaceOpenResult> OpenApplicationSurfaceAsync(IPage page, AgentTaskResponse task)
    {
        return await _navigator.OpenApplicationSurfaceAsync(page);
    }

    private static async Task EnsureJobLoadedAsync(IPage page, AgentTaskResponse task, string jobId)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var easyApplyModal = page.Locator(string.Join(", ", LinkedInSelectors.EasyApplyModal));
            var modalOpen = await easyApplyModal.CountAsync() > 0;
            if (modalOpen)
            {
                Console.WriteLine("[Navigation] Easy Apply modal is already open. Job loaded.");
                return;
            }

            var rightPanel = page.Locator(LinkedInSelectors.JobDetailPanel);
            var panelCount = await rightPanel.CountAsync();
            var panelText = panelCount > 0 ? await rightPanel.First.InnerTextAsync() : "";
            var hasJobTitle = panelText?.Contains(task.Lead.Title, StringComparison.OrdinalIgnoreCase) == true;
            Console.WriteLine($"[Navigation] Attempt {attempt + 1}: Right panel has job title: {hasJobTitle}");

            if (hasJobTitle)
                return;

            var jobLinkSelectors = new[]
            {
                $".jobs-search-results-list a[href*='{jobId}']",
                $".scaffold-layout__list a[href*='{jobId}']",
                $".jobs-search-results__list-item a[href*='{jobId}']"
            };

            var clicked = false;
            foreach (var selector in jobLinkSelectors)
            {
                var jobLink = page.Locator(selector).First;
                if (await jobLink.CountAsync() == 0)
                    continue;

                Console.WriteLine($"[Navigation] Clicking job {jobId} with selector: {selector}");
                await jobLink.ClickAsync();
                await page.WaitForTimeoutAsync(3000);
                clicked = true;
                break;
            }

            if (!clicked)
            {
                Console.WriteLine($"[Navigation] Could not find job {jobId} in left panel to click");
                return;
            }
        }
    }

    public static string? ExtractJobId(string url)
    {
        var match = Regex.Match(url, @"/jobs/view/(\d+)");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(url, @"currentJobId=(\d+)");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(url, @"referenceJobId=(\d+)");
        if (match.Success) return match.Groups[1].Value;
        return null;
    }

    public static string? ExtractDisplayedJobId(string url)
    {
        var match = Regex.Match(url, @"/jobs/view/(\d+)");
        if (match.Success) return match.Groups[1].Value;
        match = Regex.Match(url, @"currentJobId=(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string? ExtractReferenceJobId(string url)
    {
        var match = Regex.Match(url, @"referenceJobId=(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
