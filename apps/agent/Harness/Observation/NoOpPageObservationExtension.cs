using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Harness.Observation;

public sealed class NoOpPageObservationExtension : IPageObservationExtension
{
    public static NoOpPageObservationExtension Instance { get; } = new();

    private NoOpPageObservationExtension()
    {
    }

    public bool CanApply(string url, string title) => true;
    public ActiveSurfaceDefinition ActiveSurface => ActiveSurfaceDefinition.Generic;
    public PageObservation Normalize(PageObservation observation) => observation;
}
