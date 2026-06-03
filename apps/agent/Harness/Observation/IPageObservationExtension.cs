using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Harness.Observation;

public interface IPageObservationExtension
{
    bool CanApply(string url, string title);
    ActiveSurfaceDefinition ActiveSurface { get; }
    PageObservation Normalize(PageObservation observation);
}
