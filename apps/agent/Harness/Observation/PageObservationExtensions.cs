using JobPilot.Agent.Platforms.LinkedIn;

namespace JobPilot.Agent.Harness.Observation;

public static class PageObservationExtensions
{
    public static readonly IPageObservationExtension[] Default =
    [
        new LinkedInObservationExtension()
    ];
}
