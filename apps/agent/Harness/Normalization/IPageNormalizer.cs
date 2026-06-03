using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Harness.Normalization;

public interface IPageNormalizer
{
    bool CanNormalize(PageObservation observation);
    PageObservation Normalize(PageObservation observation);
}
