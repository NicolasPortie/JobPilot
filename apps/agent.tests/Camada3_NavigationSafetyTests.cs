using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class Camada3_NavigationSafetyTests
{
    [Fact]
    public void LooksLikeNextElement_NaoTrataVoltarRevisarComoAvanco()
    {
        var footerButton = new ElementCandidate(
            "back-review", "button", "", "", "Voltar Revisar", "Voltar Revisar", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        Assert.False(PageStateClassifier.LooksLikeNextElement(footerButton));
    }

    [Fact]
    public void LooksLikeNextElement_PermiteAvancarMesmoComVoltarNoContexto()
    {
        var nextButton = new ElementCandidate(
            "next", "button", "", "", "Avancar para proxima etapa Curriculo Voltar Avancar", "Avancar", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        Assert.True(PageStateClassifier.LooksLikeNextElement(nextButton));
    }

    [Fact]
    public void LooksLikeSubmitSafe_NaoTrataVoltarEnviarComoSubmit()
    {
        Assert.False(PageStateClassifier.LooksLikeSubmitSafe("Voltar Enviar candidatura"));
    }

    [Fact]
    public void LooksLikeSubmitElement_PermiteEnviarMesmoComVoltarNoContexto()
    {
        var submitButton = new ElementCandidate(
            "submit", "button", "", "", "Voltar para etapa anterior Revise sua candidatura Voltar Enviar candidatura", "Enviar candidatura", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        Assert.True(PageStateClassifier.LooksLikeSubmitElement(submitButton));
    }

    [Fact]
    public void LooksLikeReviewEditElement_ReconheceEditarNaTelaDeRevisao()
    {
        var editButton = new ElementCandidate(
            "edit", "button", "", "", "Editar Perguntas adicionais Revise sua candidatura Perguntas adicionais Editar", "Editar", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        Assert.True(PageStateClassifier.LooksLikeReviewEditElement(editButton));
        Assert.False(PageStateClassifier.LooksLikeNextElement(editButton));
        Assert.False(PageStateClassifier.LooksLikeSubmitElement(editButton));
    }

    [Fact]
    public void IsConfirmation_ReconheceSucessoDoLinkedInEmPortugues()
    {
        var observation = new PageObservation(
            "https://www.linkedin.com/jobs/search/post-apply/next-best-action/?currentJobId=1",
            "LinkedIn",
            "Sua candidatura na Carefy foi enviada.",
            [],
            HasModal: true);

        Assert.True(PageStateClassifier.IsConfirmation(observation));
    }
}
