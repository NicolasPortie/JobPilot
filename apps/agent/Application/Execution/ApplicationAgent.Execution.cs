using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Audit;
using JobPilot.Agent.Infrastructure;
using JobPilot.Agent.Platforms;
using JobPilot.Agent.Platforms.Inhire;
using JobPilot.Agent.Platforms.LinkedIn;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public sealed partial class ApplicationAgent
{
    private Task<ActionExecutionResult> ExecuteApprovedActionAsync(IBrowserDriver driver, IPage? page, PageObservation observation, ApprovedAction approvedAction, AgentTaskResponse task, SessionContext session) =>
        ExecuteActionAsync(driver, page, observation, approvedAction.ToBrowserAction(), task, session);

    private async Task<ActionExecutionResult> ExecuteActionAsync(IBrowserDriver driver, IPage? page, PageObservation observation, BrowserAction action, AgentTaskResponse task, SessionContext session)
    {
        if (action.Type == "scroll")
        {
            await driver.ScrollAsync();
            return ActionExecutionResult.Ok("Scrolled.");
        }
        if (action.Type == "wait")
        {
            if (driver is BrowserExtensionDriver)
                await Task.Delay(1500);
            else if (page is not null)
                await page.WaitForTimeoutAsync(1500);
            else
                return ActionExecutionResult.Fail("browser_page_missing", "Playwright page is required for this browser driver.");
            return ActionExecutionResult.Ok("Waited.");
        }

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (element is null)
            return ActionExecutionResult.Fail("element_not_observed", "Action target was not in the current DOM observation.");

        var extensionDriver = driver is BrowserExtensionDriver;
        var playwrightPage = page;
        if (!extensionDriver && playwrightPage is null)
            return ActionExecutionResult.Fail("browser_page_missing", "Playwright page is required for this browser driver.");
        var locator = extensionDriver ? null : playwrightPage!.Locator($"[data-jobpilot-id='{action.ElementId}']:visible").First;
        try
        {
            switch (action.Type)
            {
                case "fill":
                    var valueSource = ResolveValueSource(element.SearchText, task, action.Reason);
                    if (RequiresExplicitSource(element, valueSource))
                        return ActionExecutionResult.Fail("sensitive_answer_missing_source", "Sensitive field requires an explicit profile or reusable answer source.");

                    await driver.FillAsync(action.ElementId, action.Value);

                    // === SELF-HEALING: validar pos-preenchimento ===
                    var validation = extensionDriver
                        ? ActionExecutionResult.Ok("Filled via browser extension.")
                        : await _postActionValidator.ValidateFillAsync(playwrightPage!, locator!, element, action.Value, session);
                    if (!validation.Success)
                    {
                        return validation;
                    }

                    var storedValue = RedactIfSensitive(element, action.Value);
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, element.TypeOrTag, valueSource, action.Confidence));
                    await _client.ApplicationAnswerAsync(new ApplicationAnswerRequest(task.Task.Id, element.Label, storedValue, valueSource, "local-resolver", action.Confidence));
                    session.Record(StableFieldKey(element.SearchText), action.Value, SemanticFieldType.Unknown);
                    if (valueSource == "ai-draft")
                        await RegisterAnswerReviewAsync(task.Task.Id, element, storedValue, action.Confidence);
                    break;
                case "select":
                    await driver.SelectAsync(action.ElementId, action.Value);
                    var selectionValidation = extensionDriver
                        ? ActionExecutionResult.Ok("Selected via browser extension.")
                        : await _postActionValidator.ValidateSelectionAsync(playwrightPage!, locator!, element, action.Value);
                    if (!selectionValidation.Success)
                        return selectionValidation;
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "select", "observed-selection", action.Confidence));
                    break;
                case "check":
                    await driver.CheckAsync(action.ElementId);
                    if (!extensionDriver)
                    {
                        var checkedState = await locator!.IsCheckedAsync(new LocatorIsCheckedOptions { Timeout = 2000 });
                        if (!checkedState)
                            return ActionExecutionResult.Fail("check_not_applied", "Checkbox/radio was not checked after action.");
                    }
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "checkbox", "observed-check", action.Confidence));
                    break;
                case "upload_file":
                    var resumePath = task.ResolveResumePath();
                    if (ResumeAlreadySelected(observation))
                        return ActionExecutionResult.Ok("Resume already selected.");
                    if (extensionDriver)
                        return ActionExecutionResult.Fail("resume_upload_requires_manual_selection", ManualResumeUploadMessage(resumePath));
                    if (string.IsNullOrWhiteSpace(resumePath) || !File.Exists(resumePath))
                        return ActionExecutionResult.Fail("resume_file_missing", $"Configured resume file was not found at {resumePath}.");
                    Console.WriteLine($"[UPLOAD] Procurando inputs file... encontrados={observation.Elements.Count(e => e.IsFile)}; alvo={element.Label}");
                    await driver.UploadFileAsync(action.ElementId, resumePath);
                    if (!extensionDriver && !await AnyFileInputHasFileAsync(playwrightPage!))
                        return ActionExecutionResult.Fail("resume_upload_not_confirmed", "Resume upload action ran, but no file input reports an attached file.");
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "file", "resume-path", action.Confidence));
                    break;
                case "click":
                case "submit":
                    if (extensionDriver && action.Type == "click" && LooksLikeUploadTrigger(action, element) && !ResumeAlreadySelected(observation))
                        return ActionExecutionResult.Fail("resume_upload_requires_manual_selection", ManualResumeUploadMessage(task.ResolveResumePath()));

                    if (!extensionDriver && action.Type == "click" && LooksLikeUploadTrigger(action, element))
                    {
                        var fallbackResumePath = task.ResolveResumePath();
                        if (ResumeAlreadySelected(observation))
                            return ActionExecutionResult.Ok("Resume already selected.");
                        if (string.IsNullOrWhiteSpace(fallbackResumePath) || !File.Exists(fallbackResumePath))
                            return ActionExecutionResult.Fail("resume_file_missing", $"Configured resume file was not found at {fallbackResumePath}.");

                        var chooserAttached = await AttachResumeUsingFileChooserAsync(playwrightPage!, action.ElementId, fallbackResumePath);
                        if (!chooserAttached)
                            return ActionExecutionResult.Fail("resume_filechooser_failed", "Upload button did not expose a Playwright file chooser.");

                        await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "file", "resume-path-filechooser", action.Confidence));
                        break;
                    }

                    if (BrowserActionGuard.BlocksNavigation(observation, action, out var blockedReason))
                        return ActionExecutionResult.Fail("required_fields_block_navigation", blockedReason);

                    if (!extensionDriver && action.Type == "click" && element.Href.Contains("/apply/", StringComparison.OrdinalIgnoreCase))
                        await NavigateToApplyAsync(playwrightPage!, element.Href);
                    else
                    {
                        try { await driver.ClickAsync(action.ElementId); }
                        catch when (!string.IsNullOrWhiteSpace(element.Href) && action.Type == "click")
                        { await NavigateToApplyAsync(playwrightPage!, element.Href); }
                    }
                    if (!extensionDriver)
                    {
                        var navigationValidation = await _postActionValidator.ValidateNavigationActionAsync(playwrightPage!, observation, action);
                        if (!navigationValidation.Success)
                            return navigationValidation;
                    }
                    break;
                default:
                    return ActionExecutionResult.Fail("unsupported_action", $"Unsupported action '{action.Type}'.");
            }
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Fail("action_failed", ex.Message);
        }

        return ActionExecutionResult.Ok("Executed.");
    }

    private static async Task<bool> AttachResumeUsingFileChooserAsync(IPage page, string elementId, string resumePath)
    {
        try
        {
            Console.WriteLine($"[UPLOAD] Sem input file planejado; tentando fallback filechooser no botao: element={elementId}; file={Path.GetFileName(resumePath)}");
            var uploadButton = page.Locator($"[data-jobpilot-id='{elementId}']:visible").First;
            var fileChooser = await page.RunAndWaitForFileChooserAsync(
                async () => await uploadButton.ClickAsync(new LocatorClickOptions { Timeout = 6000 }),
                new PageRunAndWaitForFileChooserOptions { Timeout = 5000 });

            await fileChooser.SetFilesAsync(resumePath);
            await page.WaitForTimeoutAsync(600);
            var attached = await AnyFileInputHasFileAsync(page);
            Console.WriteLine($"[UPLOAD] Filechooser processado: attached={attached}");
            return attached || await PageShowsFileNameAsync(page, resumePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD] Falha no fallback filechooser: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> AnyFileInputHasFileAsync(IPage page)
    {
        try
        {
            var attachedCount = await page.Locator("input[type='file']").EvaluateAllAsync<int>(
                "els => els.reduce((sum, el) => sum + ((el.files && el.files.length) || 0), 0)");
            return attachedCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> PageShowsFileNameAsync(IPage page, string resumePath)
    {
        try
        {
            var fileName = Path.GetFileName(resumePath);
            var text = await PageObserver.GetVisibleTextAsync(page);
            return AgentText.ContainsAny(text, [fileName, ".pdf", ".doc", ".docx"]);
        }
        catch
        {
            return false;
        }
    }

    private static async Task NavigateToApplyAsync(IPage page, string href)
    {
        try
        {
            await page.GotoAsync(href, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 12000 });
        }
        catch when (page.Url.Contains("/apply/", StringComparison.OrdinalIgnoreCase) || page.Url.Contains("openSDUIApplyFlow", StringComparison.OrdinalIgnoreCase))
        {
            await page.WaitForTimeoutAsync(5000);
        }
    }

    private static async Task<IPage> SelectBestPageAsync(IPage current)
    {
        await current.WaitForTimeoutAsync(1800);
        var pages = current.Context.Pages.Where(p => !p.IsClosed).ToList();
        var external = pages.LastOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.Url) &&
            !p.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs-tracker", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs/view", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs/search", StringComparison.OrdinalIgnoreCase));

        var selected = external ?? pages.LastOrDefault(p =>
            !p.Url.Contains("jobs-tracker", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase)) ?? current;

        if (selected != current)
        {
            await selected.BringToFrontAsync();
            try { await selected.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 8000 }); }
            catch { await selected.WaitForTimeoutAsync(1000); }
        }
        return selected;
    }

}
