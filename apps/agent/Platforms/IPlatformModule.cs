using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using Microsoft.Playwright;

namespace JobPilot.Agent.Platforms;

public sealed record PlatformCheckpoint(
    string ReasonCode,
    string Stage,
    string FieldLabel,
    string Detail,
    string SuggestedUserAction = "",
    string ResumeMode = "reobserve");

public interface IPlatformModule
{
    string Name { get; }
    IApplicationPlatformFlow Flow { get; }
    IPageObservationExtension ObservationExtension { get; }
    bool CanHandle(JobLead lead);
    Task<PlatformCheckpoint?> EnsureReadyAsync(IPage page, Guid? applicationJobId, string stage, PlatformCredential? credential = null) =>
        Task.FromResult<PlatformCheckpoint?>(null);
    RecoveryDecision Recover(ActionOutcome outcome, DecisionFrame frame) =>
        new(RecoveryMode.None, "No platform-specific recovery.", true);
}

public sealed class PlatformRegistry
{
    private readonly IPlatformModule[] _modules;
    private readonly IPlatformModule _generic;

    public PlatformRegistry(IEnumerable<IPlatformModule> modules, IPlatformModule? generic = null)
    {
        _generic = generic ?? GenericPlatformModule.Instance;
        _modules = modules
            .Where(module => !ReferenceEquals(module, _generic))
            .Append(_generic)
            .ToArray();
    }

    public IPlatformModule Resolve(JobLead lead) =>
        _modules.FirstOrDefault(module => module.CanHandle(lead)) ?? _generic;

    public IReadOnlyList<IPageObservationExtension> ObservationExtensions =>
        _modules.Select(module => module.ObservationExtension).ToArray();
}
