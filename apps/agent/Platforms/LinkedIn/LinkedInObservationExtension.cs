using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInObservationExtension : IPageObservationExtension
{
    private readonly LinkedInPageNormalizer _normalizer = new();

    public bool CanApply(string url, string title) =>
        url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase);

    public ActiveSurfaceDefinition ActiveSurface { get; } = new(
        "#artdeco-modal-outlet",
        [".artdeco-modal", ".artdeco-modal__content", "[role=\"dialog\"]"],
        ["[role=\"dialog\"]", "[role=\"alertdialog\"]", ".modal", ".modal-dialog", ".overlay__content"],
        [".artdeco-modal__content", "[class*=\"jobs-easy-apply-content\"]", "[role=\"dialog\"]"],
        ["[role=\"group\"]", "fieldset", ".jobs-easy-apply-form-section__grouping", ".artdeco-modal__content", "form"]);

    public PageObservation Normalize(PageObservation observation) =>
        _normalizer.Normalize(observation);
}
