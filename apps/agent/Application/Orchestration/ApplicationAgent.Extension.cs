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
    private async Task<(PageObservation Observation, ApplicationSurfaceOpenResult? Failure)> EnsureLinkedInExpectedJobInExtensionAsync(
        BrowserExtensionHttpTransport transport,
        AgentTaskResponse task,
        string targetUrl,
        PageObservation observation)
    {
        var expectedJobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        if (string.IsNullOrWhiteSpace(expectedJobId))
            return (observation, null);

        var displayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(observation.Url);
        if (string.IsNullOrWhiteSpace(displayedJobId) || displayedJobId == expectedJobId)
            return (observation, null);

        await _client.EventAsync(task.Task.Id, "linkedin.job_mismatch_retry",
            $"expected={expectedJobId}; displayed={displayedJobId}; observedUrl={observation.Url}; retryUrl={targetUrl}");
        await transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: targetUrl, ReasonCode: "pre_apply.job_mismatch_retry"));
        await Task.Delay(2500);
        var retryObservation = await transport.ObserveAsync();
        var retryDisplayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(retryObservation.Url);
        if (string.IsNullOrWhiteSpace(retryDisplayedJobId) || retryDisplayedJobId == expectedJobId)
            return (retryObservation, null);

        var referenceJobId = LinkedInApplicationFlow.ExtractReferenceJobId(retryObservation.Url);
        var detail = referenceJobId == expectedJobId
            ? $"LinkedIn redirected from expected job {expectedJobId} to similar job {retryDisplayedJobId}; JobPilot stopped to avoid applying to the wrong vacancy."
            : $"LinkedIn opened job {retryDisplayedJobId}, but JobPilot expected {expectedJobId}.";

        return (retryObservation, new ApplicationSurfaceOpenResult(
            false,
            "job_mismatch",
            "needs_review",
            "Vaga",
            detail,
            FinalUrl: retryObservation.Url));
    }

    private async Task<ApplicationSurfaceOpenResult> OpenLinkedInSurfaceWithExtensionAsync(BrowserExtensionHttpTransport transport, AgentTaskResponse task, PageObservation initialObservation)
    {
        var jobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        try
        {
            var result = await transport.ExecuteAsync(new BrowserExtensionCommand(
                "open_apply_surface",
                ElementId: jobId ?? "",
                Value: task.Lead.Title,
                ReasonCode: "pre_apply.open_surface"));

            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            var ok = ReadBool(root, "ok", true);
            var opened = ReadBool(root, "opened", false);
            var surfaceKind = ReadString(root, "surfaceKind");
            var finalUrl = ReadString(root, "finalUrl");
            var detail = ReadString(root, "detail");
            var failureCode = ReadString(root, "failureCode");
            var failureState = ReadString(root, "failureState");
            var fieldLabel = ReadString(root, "fieldLabel");
            var error = ReadString(root, "error");

            if (BuildLinkedInJobMismatchSurface(jobId, finalUrl) is { } mismatchSurface)
                return mismatchSurface;

            if (!ok && (error.Contains("unsupported_command", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("element_not_found", StringComparison.OrdinalIgnoreCase)))
            {
                await _client.EventAsync(task.Task.Id, "extension.open_surface_fallback", $"Using observed elements because extension content script does not expose open_apply_surface yet. error={error}");
                return await OpenLinkedInSurfaceFromObservedElementsAsync(transport, task, initialObservation);
            }

            if (!ok || !opened)
            {
                if (failureCode == "easy_apply_modal_not_opened")
                {
                    var navigationFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, finalUrl, jobId);
                    if (navigationFallback.Opened)
                        return navigationFallback;
                    if (navigationFallback.FailureCode == "job_mismatch")
                        return navigationFallback;
                }

                return new ApplicationSurfaceOpenResult(
                    false,
                    string.IsNullOrWhiteSpace(failureCode) ? "application_surface_not_found" : failureCode,
                    string.IsNullOrWhiteSpace(failureState) ? "needs_review" : failureState,
                    string.IsNullOrWhiteSpace(fieldLabel) ? "Candidatar-se" : fieldLabel,
                    string.IsNullOrWhiteSpace(detail) ? "The extension could not open a LinkedIn application surface." : detail,
                    surfaceKind,
                    string.IsNullOrWhiteSpace(finalUrl) ? initialObservation.Url : finalUrl);
            }

            var suggestedPlatform = surfaceKind switch
            {
                "linkedin_easy_apply" => "LinkedIn",
                "inhire" => "InHire",
                "unsupported_external" => "Unsupported",
                _ => "LinkedIn"
            };

            return ApplicationSurfaceOpenResult.OpenedSurface(
                surfaceKind,
                suggestedPlatform,
                string.IsNullOrWhiteSpace(finalUrl) ? initialObservation.Url : finalUrl,
                detail,
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _client.EventAsync(task.Task.Id, "extension.open_surface_fallback", $"Using observed elements because extension content script does not expose open_apply_surface yet. error={ex.Message}");
            try
            {
                return await OpenLinkedInSurfaceFromObservedElementsAsync(transport, task, initialObservation);
            }
            catch (Exception fallbackEx) when (fallbackEx is not OperationCanceledException)
            {
                return new ApplicationSurfaceOpenResult(
                    false,
                    "easy_apply_modal_not_opened",
                    "needs_review",
                    "Candidatura simplificada",
                    $"The extension could not open LinkedIn Easy Apply. fallback_error={fallbackEx.Message}",
                    "linkedin_easy_apply",
                    initialObservation.Url);
            }
        }
    }

    private static async Task<ApplicationSurfaceOpenResult> TryOpenLinkedInEasyApplyByHrefAsync(BrowserExtensionHttpTransport transport, PageObservation observation, string fallbackUrl, string? expectedJobId)
    {
        try
        {
            return await OpenLinkedInEasyApplyByHrefAsync(transport, observation, fallbackUrl, expectedJobId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "easy_apply_modal_not_opened",
                "needs_review",
                "Candidatura simplificada",
                $"Easy Apply href fallback failed. error={ex.Message}",
                "linkedin_easy_apply",
                string.IsNullOrWhiteSpace(fallbackUrl) ? observation.Url : fallbackUrl);
        }
    }

    private static async Task<ApplicationSurfaceOpenResult> OpenLinkedInEasyApplyByHrefAsync(BrowserExtensionHttpTransport transport, PageObservation observation, string fallbackUrl, string? expectedJobId)
    {
        var easyApplyLink = observation.Elements.FirstOrDefault(e =>
            e.IsButtonLike &&
            !string.IsNullOrWhiteSpace(e.Href) &&
            e.Href.Contains("/apply/", StringComparison.OrdinalIgnoreCase) &&
            (e.Href.Contains("openSDUIApplyFlow", StringComparison.OrdinalIgnoreCase) ||
             AgentText.ContainsAny(e.SearchText, ["easy apply", "candidatura simplificada"])));

        if (easyApplyLink is null)
            return new ApplicationSurfaceOpenResult(false);

        await transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: easyApplyLink.Href, ReasonCode: "pre_apply.easy_apply_href"));
        await Task.Delay(2500);
        var afterNavigation = await transport.ObserveAsync();
        if (BuildLinkedInJobMismatchSurface(expectedJobId, afterNavigation.Url) is { } mismatchSurface)
            return mismatchSurface;

        if (LooksLikeEasyApplyModal(afterNavigation))
        {
            return ApplicationSurfaceOpenResult.OpenedSurface(
                "linkedin_easy_apply",
                "LinkedIn",
                afterNavigation.Url,
                "LinkedIn Easy Apply modal opened by navigating to the apply href.");
        }

        return new ApplicationSurfaceOpenResult(
            false,
            "easy_apply_modal_not_opened",
            "needs_review",
            easyApplyLink.Label,
            "Easy Apply href navigation ran, but the modal did not open.",
            "linkedin_easy_apply",
            string.IsNullOrWhiteSpace(afterNavigation.Url) ? fallbackUrl : afterNavigation.Url);
    }

    private static ApplicationSurfaceOpenResult? BuildLinkedInJobMismatchSurface(string? expectedJobId, string observedUrl)
    {
        if (string.IsNullOrWhiteSpace(expectedJobId))
            return null;

        var displayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(observedUrl);
        if (string.IsNullOrWhiteSpace(displayedJobId) || displayedJobId == expectedJobId)
            return null;

        var referenceJobId = LinkedInApplicationFlow.ExtractReferenceJobId(observedUrl);
        var detail = referenceJobId == expectedJobId
            ? $"LinkedIn redirected from expected job {expectedJobId} to similar job {displayedJobId}; JobPilot stopped to avoid applying to the wrong vacancy."
            : $"LinkedIn opened job {displayedJobId}, but JobPilot expected {expectedJobId}.";

        return new ApplicationSurfaceOpenResult(
            false,
            "job_mismatch",
            "needs_review",
            "Vaga",
            detail,
            FinalUrl: observedUrl);
    }

    private async Task<ApplicationSurfaceOpenResult> OpenLinkedInSurfaceFromObservedElementsAsync(BrowserExtensionHttpTransport transport, AgentTaskResponse task, PageObservation initialObservation)
    {
        var jobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        var observedJobId = LinkedInApplicationFlow.ExtractJobId(initialObservation.Url);
        if (!string.IsNullOrWhiteSpace(jobId) &&
            !string.IsNullOrWhiteSpace(observedJobId) &&
            !jobId.Equals(observedJobId, StringComparison.OrdinalIgnoreCase))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "job_mismatch",
                "needs_review",
                "Vaga",
                $"LinkedIn opened job {observedJobId}, but JobPilot expected {jobId}.",
                FinalUrl: initialObservation.Url);
        }

        if (!TitleLooksVisible(initialObservation, task.Lead.Title))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "job_title_not_visible",
                "needs_review",
                "Vaga",
                "The expected job title is not visible in the LinkedIn page opened by the extension.",
                FinalUrl: initialObservation.Url);
        }

        if (LinkedInPageShowsAlreadyApplied(initialObservation))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "already_applied",
                "applied",
                "Candidatura",
                "LinkedIn indicates this job was already applied.",
                "already_applied",
                initialObservation.Url);
        }

        var alreadyApplied = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && AgentText.ContainsAny(e.SearchText, ["applied", "candidatura enviada", "voce se candidatou", "ja se candidatou"]));
        if (alreadyApplied is not null)
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "already_applied",
                "applied",
                "Candidatura",
                "LinkedIn indicates this job was already applied.",
                "already_applied",
                initialObservation.Url);
        }

        var easyApply = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && AgentText.ContainsAny(e.SearchText, ["easy apply", "candidatura simplificada", "candidate-se simplificadamente"]));
        if (easyApply is not null)
        {
            PageObservation afterClick;
            try
            {
                await transport.DispatchAsync(new BrowserExtensionCommand("click", easyApply.ElementId, ReasonCode: "pre_apply.easy_apply"));
                await Task.Delay(2500);
                afterClick = await transport.ObserveAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var clickFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, initialObservation.Url, jobId);
                if (clickFallback.Opened)
                    return clickFallback;
                if (clickFallback.FailureCode == "job_mismatch")
                    return clickFallback;

                return new ApplicationSurfaceOpenResult(
                    false,
                    "easy_apply_modal_not_opened",
                    "needs_review",
                    easyApply.Label,
                    $"Easy Apply was present, but the extension could not click it. error={ex.Message}",
                    "linkedin_easy_apply",
                    initialObservation.Url);
            }

            if (LooksLikeEasyApplyModal(afterClick))
            {
                return ApplicationSurfaceOpenResult.OpenedSurface(
                    "linkedin_easy_apply",
                    "LinkedIn",
                    afterClick.Url,
                    "LinkedIn Easy Apply modal opened via observed extension elements.");
            }

            var hrefFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, afterClick.Url, jobId);
            if (hrefFallback.Opened)
                return hrefFallback;
            if (hrefFallback.FailureCode == "job_mismatch")
                return hrefFallback;

            return new ApplicationSurfaceOpenResult(
                false,
                "easy_apply_modal_not_opened",
                "needs_review",
                easyApply.Label,
                "Easy Apply was present, but the modal did not open.",
                "linkedin_easy_apply",
                afterClick.Url);
        }

        var externalApply = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && LooksLikeLinkedInExternalApply(e));
        if (externalApply is not null)
        {
            await transport.DispatchAsync(new BrowserExtensionCommand("click", externalApply.ElementId, ReasonCode: "pre_apply.external_apply"));
            await Task.Delay(3500);
            var afterClick = await transport.ObserveAsync();
            var finalUrl = string.IsNullOrWhiteSpace(afterClick.Url) ? externalApply.Href : afterClick.Url;
            var surfaceKind = finalUrl.Contains("inhire.app", StringComparison.OrdinalIgnoreCase) ? "inhire" : "unsupported_external";
            return ApplicationSurfaceOpenResult.OpenedSurface(
                surfaceKind,
                surfaceKind == "inhire" ? "InHire" : "Unsupported",
                finalUrl,
                surfaceKind == "inhire"
                    ? "LinkedIn external apply opened an InHire surface via observed extension elements."
                    : "LinkedIn job uses an external application site that JobPilot does not automate yet.");
        }

        return new ApplicationSurfaceOpenResult(
            false,
            "application_surface_not_found",
            "needs_review",
            "Candidatar-se",
            "No Easy Apply or external apply button was found in the LinkedIn job detail panel.",
            FinalUrl: initialObservation.Url);
    }

}

