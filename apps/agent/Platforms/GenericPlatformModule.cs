using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;

namespace JobPilot.Agent.Platforms;

public sealed class GenericPlatformModule : IPlatformModule
{
    public static GenericPlatformModule Instance { get; } = new();

    private GenericPlatformModule()
    {
    }

    public string Name => "Generic";
    public IApplicationPlatformFlow Flow => GenericApplicationPlatformFlow.Instance;
    public IPageObservationExtension ObservationExtension => NoOpPageObservationExtension.Instance;

    public bool CanHandle(JobLead lead) => true;

    public RecoveryDecision Recover(ActionOutcome outcome, DecisionFrame frame)
    {
        if (outcome.FailureCode?.Contains("not_observed", StringComparison.OrdinalIgnoreCase) == true)
            return new RecoveryDecision(RecoveryMode.Reobserve, "Generic page changed; reobserve before trying another action.", true);

        return new RecoveryDecision(
            outcome.Success ? RecoveryMode.None : RecoveryMode.Checkpoint,
            outcome.Success ? outcome.Message : "Generic platform cannot safely infer a site-specific recovery.",
            outcome.Success,
            outcome.Success ? "" : "Review the current form state and resolve the checkpoint.",
            "reobserve");
    }
}

