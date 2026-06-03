using JobPilot.Agent.Application;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using JobPilot.Agent.Platforms;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInPlatformModule : IPlatformModule
{
    private readonly LinkedInApplicationFlow _flow;
    private readonly LinkedInNavigator _navigator;
    private readonly LinkedInObservationExtension _observation = new();
    private static readonly string[] CaptchaSignals = ["captcha", "i'm not a robot", "nao sou um robo", "puzzle", "desafio de seguranca"];

    public LinkedInPlatformModule(LinkedInNavigator navigator)
    {
        _navigator = navigator;
        _flow = new LinkedInApplicationFlow(navigator);
    }

    public string Name => "LinkedIn";
    public IApplicationPlatformFlow Flow => _flow;
    public IPageObservationExtension ObservationExtension => _observation;

    public bool CanHandle(JobLead lead) =>
        lead.Platform.Equals("linkedin", StringComparison.OrdinalIgnoreCase) ||
        lead.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase);

    public async Task<PlatformCheckpoint?> EnsureReadyAsync(IPage page, Guid? applicationJobId, string stage, PlatformCredential? credential = null)
    {
        if (await _navigator.NeedsLoginAsync(page))
        {
            if (await _navigator.TryLoginAsync(page, credential?.AccountEmail, credential?.Password))
                return null;

            return new PlatformCheckpoint(
                "login_required",
                stage,
                "",
                "Log in manually in the opened browser, then resume the agent.",
                "Log in to LinkedIn in the browser, then resume from a fresh observation.",
                "reobserve");
        }

        var visibleText = AgentText.RemoveDiacritics(await PageObserver.GetVisibleTextAsync(page)).ToLowerInvariant();
        if (page.Url.Contains("checkpoint/challenge", StringComparison.OrdinalIgnoreCase) || CaptchaSignals.Any(visibleText.Contains))
        {
            return new PlatformCheckpoint(
                "captcha_or_security",
                stage,
                "",
                "LinkedIn security challenge detected. Manual action required.",
                "Resolve the LinkedIn security challenge in the browser, then resume from a fresh observation.",
                "reobserve");
        }

        return null;
    }

    public RecoveryDecision Recover(ActionOutcome outcome, DecisionFrame frame)
    {
        if (outcome.FailureCode is "navigation_no_state_change" or "submit_no_state_change")
        {
            return new RecoveryDecision(
                RecoveryMode.Reobserve,
                "LinkedIn Easy Apply often keeps the same URL while modal content changes; reobserve active surface.",
                true,
                "",
                "reobserve");
        }

        if (outcome.FailureCode?.Contains("selection", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new RecoveryDecision(
                RecoveryMode.Remap,
                "LinkedIn custom select may need normalized option remapping.",
                true,
                "",
                "retry_action");
        }

        return new RecoveryDecision(
            outcome.Success ? RecoveryMode.None : RecoveryMode.Checkpoint,
            outcome.Message,
            outcome.Success,
            outcome.Success ? "" : "Review the LinkedIn Easy Apply modal and resolve the checkpoint.",
            "reobserve");
    }
}
