using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using JobPilot.Agent.Platforms;

namespace JobPilot.Agent.Application;

public sealed class ApplicationPlatformRouter
{
    private readonly PlatformRegistry? _registry;
    private readonly IApplicationPlatformFlow[] _flows;

    public ApplicationPlatformRouter(params IApplicationPlatformFlow[] flows)
    {
        _flows = flows;
    }

    public ApplicationPlatformRouter(PlatformRegistry registry)
    {
        _registry = registry;
        _flows = [];
    }

    public IApplicationPlatformFlow Resolve(JobLead lead) =>
        _registry?.Resolve(lead).Flow ??
        _flows.FirstOrDefault(flow => flow.CanHandle(lead)) ??
        GenericApplicationPlatformFlow.Instance;

    public IPlatformModule ResolveModule(JobLead lead) =>
        _registry?.Resolve(lead) ??
        (_flows.FirstOrDefault(flow => flow.CanHandle(lead)) is { } flow
            ? new LegacyFlowPlatformModule(flow)
            : GenericPlatformModule.Instance);

    private sealed class LegacyFlowPlatformModule : IPlatformModule
    {
        public LegacyFlowPlatformModule(IApplicationPlatformFlow flow) => Flow = flow;
        public string Name => Flow.GetType().Name.Replace("ApplicationFlow", "");
        public IApplicationPlatformFlow Flow { get; }
        public IPageObservationExtension ObservationExtension => NoOpPageObservationExtension.Instance;
        public bool CanHandle(JobLead lead) => Flow.CanHandle(lead);
    }
}
