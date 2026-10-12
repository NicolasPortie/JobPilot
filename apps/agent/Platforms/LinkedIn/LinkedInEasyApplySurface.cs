using System.Text.RegularExpressions;
using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInEasyApplySurface
{
    public async Task<ApplicationSurfaceOpenResult> OpenAsync(IPage page)
    {
        var visibleText = AgentText.RemoveDiacritics(await PageObserver.GetVisibleTextAsync(page)).ToLowerInvariant();
        if (AgentText.ContainsAny(visibleText, ["candidatura enviada", "voce se candidatou", "você se candidatou", "application submitted", "applied"]))
        {
            Console.WriteLine("[EasyApply] Job appears to be already applied.");
            return new ApplicationSurfaceOpenResult(
                false,
                "already_applied",
                "applied",
                "Candidatura",
                "LinkedIn indicates this job was already applied.",
                "already_applied",
                page.Url,
                "LinkedIn",
                page);
        }

        var allButtons = page.Locator("button");
        var btnCount = await allButtons.CountAsync();
        for (var index = 0; index < Math.Min(btnCount, 15); index++)
        {
            var button = allButtons.Nth(index);
            var text = await button.TextContentAsync() ?? "";
            var box = await button.BoundingBoxAsync();
            if (!string.IsNullOrWhiteSpace(text))
                Console.WriteLine($"[EasyApply] Button {index}: x={box?.X}, y={box?.Y}, text='{text.Trim()}'");
        }

        var rightPanel = page.Locator(LinkedInSelectors.JobDetailPanel);
        if (await rightPanel.CountAsync() > 0)
        {
            var easyApplyBtn = rightPanel.First.Locator(LinkedInSelectors.EasyApplyButton).First;
            if (await easyApplyBtn.CountAsync() > 0)
            {
                return await OpenEasyApplyAsync(page, easyApplyBtn, "right panel");
            }

            var externalApply = rightPanel.First.Locator(LinkedInSelectors.ExternalApplyButton).First;
            if (await externalApply.CountAsync() > 0)
            {
                return await OpenExternalApplyButtonAsync(page, externalApply);
            }
        }

        var globalEasyApply = page.Locator(LinkedInSelectors.EasyApplyButton).First;
        if (await globalEasyApply.CountAsync() > 0)
            return await OpenEasyApplyAsync(page, globalEasyApply, "page fallback");

        var globalExternalApply = page.Locator(LinkedInSelectors.ExternalApplyButton).First;
        if (await globalExternalApply.CountAsync() > 0)
            return await OpenExternalApplyButtonAsync(page, globalExternalApply);

        Console.WriteLine("[EasyApply] No Easy Apply or external apply button found.");
        return new ApplicationSurfaceOpenResult(
            false,
            "application_surface_not_found",
            "needs_review",
            "Candidatar-se",
            "No Easy Apply or external apply button was found in the LinkedIn job detail panel.");
    }

    public async Task<bool> ClickEasyApplyAsync(IPage page) =>
        (await OpenAsync(page)).SurfaceKind == "linkedin_easy_apply";

    private static async Task<ApplicationSurfaceOpenResult> OpenEasyApplyAsync(IPage page, ILocator easyApplyBtn, string source)
    {
        var box = await easyApplyBtn.BoundingBoxAsync();
        Console.WriteLine($"[EasyApply] Clicking EasyApply from {source} at x={box?.X}, y={box?.Y}");
        try
        {
            await easyApplyBtn.ClickAsync(new LocatorClickOptions { Timeout = 6000 });
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine($"[EasyApply] Easy Apply click failed: {ex.Message}");
            return new ApplicationSurfaceOpenResult(
                false,
                "easy_apply_click_failed",
                "needs_review",
                "Candidatura simplificada",
                "Easy Apply button was present, but the click failed.");
        }

        if (await WaitForEasyApplyModalAsync(page))
            return ApplicationSurfaceOpenResult.Success;

        Console.WriteLine("[EasyApply] Clicked Easy Apply, but no application modal/dialog appeared.");
        return new ApplicationSurfaceOpenResult(
            false,
            "easy_apply_modal_not_opened",
            "needs_review",
            "Candidatura simplificada",
            "Easy Apply did not open an application modal/dialog.");
    }

    private static async Task<ApplicationSurfaceOpenResult> OpenExternalApplyButtonAsync(IPage page, ILocator externalApply)
    {
        Console.WriteLine("[EasyApply] Job has external apply button (Candidatar-se). Opening external application.");
        var externalUrlHint = await ExternalUrlHintAsync(externalApply);
        var external = await OpenExternalApplyAsync(page, externalApply, externalUrlHint);
        if (external.Opened)
            return external;

        return new ApplicationSurfaceOpenResult(
            false,
            "external_apply_not_opened",
            "needs_review",
            "Candidatar-se",
            "External apply button was present, but no external application page opened.");
    }

    private static async Task<ApplicationSurfaceOpenResult> OpenExternalApplyAsync(IPage page, ILocator externalApply, string externalUrlHint)
    {
        var beforeUrl = page.Url;
        var beforePages = page.Context.Pages.ToHashSet();

        try
        {
            var popup = await page.RunAndWaitForPopupAsync(
                async () => await externalApply.ClickAsync(new LocatorClickOptions { Timeout = 6000 }),
                new PageRunAndWaitForPopupOptions { Timeout = 3000 });
            await PrepareExternalPageAsync(popup);
            return ExternalSurface(popup, $"LinkedIn external apply opened a new page: {popup.Url}", externalUrlHint);
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[EasyApply] External apply did not emit popup event; checking same tab/context pages.");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[EasyApply] External apply popup wait timed out; checking same tab/context pages.");
        }

        await page.WaitForTimeoutAsync(2500);
        var newPage = page.Context.Pages.LastOrDefault(p => !beforePages.Contains(p) && !p.IsClosed);
        if (newPage is not null)
        {
            await PrepareExternalPageAsync(newPage);
            return ExternalSurface(newPage, $"LinkedIn external apply opened a new page: {newPage.Url}", externalUrlHint);
        }

        await PrepareExternalPageAsync(page);
        if (!page.Url.Equals(beforeUrl, StringComparison.OrdinalIgnoreCase) && !IsLinkedInApplyIntermediary(page.Url))
            return ExternalSurface(page, $"LinkedIn external apply navigated in the same page: {page.Url}", externalUrlHint);

        var candidate = page.Context.Pages.LastOrDefault(p => !p.IsClosed && !IsLinkedInApplyIntermediary(p.Url));
        if (candidate is not null && candidate != page)
        {
            await PrepareExternalPageAsync(candidate);
            return ExternalSurface(candidate, $"LinkedIn external apply selected existing external page: {candidate.Url}", externalUrlHint);
        }

        return new ApplicationSurfaceOpenResult(false);
    }

    private static async Task<bool> WaitForEasyApplyModalAsync(IPage page)
    {
        var modal = page.Locator(string.Join(", ", LinkedInSelectors.EasyApplyModal));
        try
        {
            await modal.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 7000 });
            Console.WriteLine("[EasyApply] Application modal/dialog detected.");
            return true;
        }
        catch
        {
            var visibleDialogs = await page.Locator("[role='dialog']:visible, #artdeco-modal-outlet:visible").CountAsync();
            Console.WriteLine($"[EasyApply] Modal wait timed out. visibleDialogs={visibleDialogs}");
            return visibleDialogs > 0;
        }
    }

    private static async Task PrepareExternalPageAsync(IPage page)
    {
        await page.BringToFrontAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (IsLinkedInApplyIntermediary(page.Url) && DateTimeOffset.UtcNow < deadline)
            await page.WaitForTimeoutAsync(150);

        try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 10000 }); }
        catch { await page.WaitForTimeoutAsync(1500); }

        deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (IsLinkedInApplyIntermediary(page.Url) && DateTimeOffset.UtcNow < deadline)
            await page.WaitForTimeoutAsync(150);
    }

    private static bool IsLinkedInApplyIntermediary(string url) =>
        string.IsNullOrWhiteSpace(url) ||
        url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("chrome-error:", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("linkedin.com/jobs-tracker", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("linkedin.com/jobs/view", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("linkedin.com/jobs/search", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("linkedin.com/jobs/collections", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ExternalUrlHintAsync(ILocator externalApply)
    {
        var href = await externalApply.GetAttributeAsync("href") ?? "";
        if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
            return href;

        var onclick = await externalApply.GetAttributeAsync("onclick") ?? "";
        var match = Regex.Match(onclick, @"https?://[^'""\s)]+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "";
    }

    private static string EffectiveUrl(IPage page, string externalUrlHint) =>
        !string.IsNullOrWhiteSpace(page.Url) && !IsLinkedInApplyIntermediary(page.Url)
            ? page.Url
            : externalUrlHint;

    private static ApplicationSurfaceOpenResult ExternalSurface(IPage page, string detail, string externalUrlHint)
    {
        var finalUrl = EffectiveUrl(page, externalUrlHint);
        if (finalUrl.Contains("inhire.app", StringComparison.OrdinalIgnoreCase))
        {
            return ApplicationSurfaceOpenResult.OpenedSurface(
                "inhire",
                "InHire",
                finalUrl,
                detail,
                page);
        }

        return ApplicationSurfaceOpenResult.OpenedSurface(
            "unsupported_external",
            "Unsupported",
            finalUrl,
            detail,
            page);
    }
}
