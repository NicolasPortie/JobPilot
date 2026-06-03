using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Audit;

namespace JobPilot.Agent.Tests;

public class Camada3_BrowserActionGuardTests
{
    [Fact]
    public void BlocksNavigation_ComComboboxObrigatorioNoPlaceholder()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "combo", "button", "", "", "Voce possui experiencia com Java?* Selecionar opcao",
                "Selecionar opcao", "Selecionar opcao", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32)),
            new ElementCandidate(
                "review", "button", "", "", "Revisar", "Revisar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("click", "review", "", "Revisar", "Advance", 0.8m);

        var blocked = BrowserActionGuard.BlocksNavigation(observation, action, out var reason);

        Assert.True(blocked);
        Assert.Contains("select", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlocksNavigation_ComErroVisivelMesmoSemCampoObservado()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn",
            "Insira uma resposta valida", [
            new ElementCandidate(
                "review", "button", "", "", "Revisar", "Revisar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("click", "review", "", "Revisar", "Advance", 0.8m);

        var blocked = BrowserActionGuard.BlocksNavigation(observation, action, out var reason);

        Assert.True(blocked);
        Assert.Contains("validation", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlocksNavigation_NaoTrataOpcaoDeDropdownComoErroVisivel()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn",
            "Codigo do pais Selecionar opcao Brasil (+55)", [
            new ElementCandidate(
                "country", "button", "", "combobox", "Codigo do pais", "Brasil (+55)", "Brasil (+55)", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32)),
            new ElementCandidate(
                "next", "button", "", "", "Avancar", "Avancar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("click", "next", "", "Avancar", "Advance", 0.8m);

        var blocked = BrowserActionGuard.BlocksNavigation(observation, action, out _);

        Assert.False(blocked);
    }

    [Fact]
    public void ElementCandidate_NaoMarcaPerguntasComoDocumentoRg()
    {
        var element = new ElementCandidate(
            "location", "input", "text", "", "What is your current location? Perguntas adicionais",
            "", "", true, false, false, "", [], new ElementBounds(0, 0, 200, 32));

        Assert.False(element.IsSensitive);
    }

    [Fact]
    public void ElementCandidate_MarcaRgComoDocumentoSensivelQuandoIsolado()
    {
        var element = new ElementCandidate(
            "rg", "input", "text", "", "Informe seu RG",
            "", "", true, false, false, "", [], new ElementBounds(0, 0, 200, 32));

        Assert.True(element.IsSensitive);
    }

    [Fact]
    public void BlocksNavigation_NaoBloqueiaUploadObrigatorioQuandoCurriculoJaSelecionado()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn",
            "Curriculo Nicolas Portie.pdf Desmarcar selecao", [
            new ElementCandidate(
                "resume", "input", "file", "", "Curriculo", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 100, 32)),
            new ElementCandidate(
                "next", "button", "", "", "Avancar", "Avancar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("click", "next", "", "Avancar", "Advance", 0.8m);

        var blocked = BrowserActionGuard.BlocksNavigation(observation, action, out _);

        Assert.False(blocked);
    }

    [Fact]
    public void BlocksNavigation_BloqueiaCoverLetterObrigatoriaMesmoComCurriculoSelecionado()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn",
            "Curriculo Nicolas Portie.pdf Desmarcar selecao", [
            new ElementCandidate(
                "cover", "input", "file", "", "Cover letter", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 100, 32)),
            new ElementCandidate(
                "next", "button", "", "", "Avancar", "Avancar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("click", "next", "", "Avancar", "Advance", 0.8m);

        var blocked = BrowserActionGuard.BlocksNavigation(observation, action, out var reason);

        Assert.True(blocked);
        Assert.Contains("file", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSafeAction_BloqueiaUploadEmBotao()
    {
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "upload-button", "button", "", "", "Carregue o curriculo", "Carregue o curriculo", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);
        var action = new BrowserAction("upload_file", "upload-button", "", "Carregue o curriculo", "LLM suggested upload.", 0.8m);

        var safe = BrowserActionGuard.IsSafeAction(observation, action, out var reason);

        Assert.False(safe);
        Assert.Contains("incompatible", reason, StringComparison.OrdinalIgnoreCase);
    }
}
