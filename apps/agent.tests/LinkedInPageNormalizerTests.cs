using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Observation;
using JobPilot.Agent.Platforms.LinkedIn;

namespace JobPilot.Agent.Tests;

public class LinkedInPageNormalizerTests
{
    [Fact]
    public void Normalize_ConverteBotaoSelecionarOpcaoEmCombobox()
    {
        var normalizer = new LinkedInPageNormalizer();
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "dropdown", "button", "", "", "Voce possui Git?* Selecionar opcao",
                "Selecionar opcao", "", false, false, false, "",
                [], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var normalized = normalizer.Normalize(observation);
        var element = normalized.Elements.Single();

        Assert.Equal("combobox", element.Role);
        Assert.True(element.IsSelect);
        Assert.True(element.Required);
        Assert.Equal("Selecionar opcao", element.Value);
    }

    [Fact]
    public void ObservationExtension_DefineSuperficieAtivaDoLinkedIn()
    {
        var extension = new LinkedInObservationExtension();

        Assert.True(extension.CanApply("https://www.linkedin.com/jobs/view/1", ""));
        Assert.Equal("#artdeco-modal-outlet", extension.ActiveSurface.ModalOutletSelector);
        Assert.Contains(".artdeco-modal__content", extension.ActiveSurface.ModalSelectors);
        Assert.Contains("[class*=\"jobs-easy-apply-content\"]", extension.ActiveSurface.ModalScrollSelectors);
    }

    [Fact]
    public void DefaultObservationExtensions_IncluiLinkedInSemAcoplarPageObserver()
    {
        Assert.Contains(PageObservationExtensions.Default, extension => extension is LinkedInObservationExtension);
    }
}

