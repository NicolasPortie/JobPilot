using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Normalization;

namespace JobPilot.Agent.Platforms.LinkedIn;

public sealed class LinkedInPageNormalizer : IPageNormalizer
{
    public bool CanNormalize(PageObservation observation) =>
        observation.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        observation.Title.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase);

    public PageObservation Normalize(PageObservation observation)
    {
        if (!CanNormalize(observation)) return observation;

        var elements = observation.Elements
            .Select(NormalizeElement)
            .ToList();

        return observation with { Elements = elements };
    }

    private static ElementCandidate NormalizeElement(ElementCandidate element)
    {
        if (!LooksLikeLinkedInCustomSelect(element))
            return element;

        var label = element.Label;
        if (string.IsNullOrWhiteSpace(label))
            label = element.Text;

        return element with
        {
            Role = string.IsNullOrWhiteSpace(element.Role) ? "combobox" : element.Role,
            Required = element.Required || AgentText.ContainsAny(label, ["*"]),
            Value = string.IsNullOrWhiteSpace(element.Value) ? element.Text : element.Value
        };
    }

    private static bool LooksLikeLinkedInCustomSelect(ElementCandidate element)
    {
        if (element.IsSelect) return true;
        if (element.Tag is not ("button" or "div" or "span")) return false;

        return AgentText.ContainsAny($"{element.Label} {element.Text} {element.Value}", [
            "selecionar opcao", "selecionar opção", "select an option", "choose"
        ]);
    }
}
