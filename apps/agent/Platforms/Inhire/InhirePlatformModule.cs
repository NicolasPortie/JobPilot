using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using JobPilot.Agent.Platforms;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms.Inhire;

public sealed class InhirePlatformModule : IPlatformModule
{
    private static readonly string[] BlockSignals = ["captcha", "nao sou um robo", "não sou um robô", "security check", "verificacao de seguranca", "verificação de segurança"];
    private readonly InhireApplicationFlow _flow = new();

    public string Name => "InHire";
    public IApplicationPlatformFlow Flow => _flow;
    public IPageObservationExtension ObservationExtension => NoOpPageObservationExtension.Instance;

    public bool CanHandle(JobLead lead) => _flow.CanHandle(lead);

    public async Task<PlatformCheckpoint?> EnsureReadyAsync(IPage page, Guid? applicationJobId, string stage, PlatformCredential? credential = null)
    {
        var text = AgentText.RemoveDiacritics(await PageObserver.GetVisibleTextAsync(page)).ToLowerInvariant();
        if (BlockSignals.Any(text.Contains))
        {
            return new PlatformCheckpoint(
                "captcha_or_security",
                stage,
                "",
                "InHire security challenge detected. Manual action required.",
                "Resolve the InHire security challenge in the browser, then resume.",
                "reobserve");
        }

        return null;
    }

    public RecoveryDecision Recover(ActionOutcome outcome, DecisionFrame frame) =>
        new(
            outcome.Success ? RecoveryMode.None : RecoveryMode.Checkpoint,
            outcome.Success ? outcome.Message : "InHire flow needs review before continuing.",
            outcome.Success,
            outcome.Success ? "" : "Review the InHire form and resolve missing or ambiguous required fields.",
            "reobserve");
}
