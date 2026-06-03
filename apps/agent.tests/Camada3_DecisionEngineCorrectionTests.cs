using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Tests;

public class Camada3_DecisionEngineCorrectionTests
{
    [Fact]
    public async Task Decide_TelefonePreenchidoErrado_CorrigeComPerfil()
    {
        var resolver = CreateResolver(new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            Phone = "11999998888",
            Email = "nicolas@email.com"
        });
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(new CandidateProfile { Phone = "11999998888" });
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "phone1", "input", "text", "", "Telefone", "", "(11) 11111-1111", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32),
                Placeholder: "(99) 99999-9999", AutoComplete: "tel")
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("fill", action.Type);
        Assert.Equal("phone1", action.ElementId);
        Assert.Equal("(11) 99999-8888", action.Value);
    }

    [Fact]
    public async Task Decide_PerguntaAnosExperiencia_SkillAusente_UsaZero()
    {
        var resolver = CreateResolver(new CandidateProfile { FullName = "Nicolas Portie Suss" });
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(new CandidateProfile());
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "years1", "input", "text", "", "Ha quantos anos voce ja usa Node.js no trabalho?", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32),
                InputMode: "numeric")
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("fill", action.Type);
        Assert.Equal("years1", action.ElementId);
        Assert.Equal("0", action.Value);
    }

    [Fact]
    public async Task Decide_ComboboxSimNao_ComSkillNoPerfil_SelecionaOpcaoCorreta()
    {
        var profile = new CandidateProfile
        {
            Skills = ["Node.js", "Git", "GitHub"],
            GithubUrl = "https://github.com/nicolas"
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "node-combo", "div", "", "combobox", "Voce possui experiencia pratica com Node.js?", "", "Selecionar opcao", true, false, false, "",
                ["Selecionar opcao", "Sim", "Nao"], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("node-combo", action.ElementId);
        Assert.Equal("Sim", action.Value);
    }

    [Fact]
    public async Task Decide_ComboboxSimNao_SkillAusente_SelecionaNao()
    {
        var profile = new CandidateProfile
        {
            Skills = ["Node.js", "Git", "GitHub"]
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "php-combo", "div", "", "combobox", "Voce possui experiencia pratica com PHP?", "", "Selecionar opcao", true, false, false, "",
                ["Selecionar opcao", "Sim", "Nao"], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("php-combo", action.ElementId);
        Assert.Equal("Nao", action.Value);
    }

    [Fact]
    public async Task Decide_ComboboxYesNo_ComConceitoBaseDoPerfil_SelecionaYes()
    {
        var profile = new CandidateProfile
        {
            Skills = ["C#", "ASP.NET Core", "SQL"],
            ProfessionalSummary = "Desenvolvedor de software com experiencia em sistemas corporativos.",
            TargetRole = "ASP.NET Developer"
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "oop-combo", "div", "", "combobox", "Possui experiencia anterior com logica de programacao e orientacao a objetos?", "", "Selecionar opcao", true, false, false, "",
                ["Selecionar opcao", "Yes", "No"], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("oop-combo", action.ElementId);
        Assert.Equal("Yes", action.Value);
    }

    [Fact]
    public async Task Decide_ComboboxYesNo_SkillAusente_SelecionaNo()
    {
        var profile = new CandidateProfile
        {
            Skills = ["C#", "ASP.NET Core", "SQL"]
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "java-combo", "div", "", "combobox", "Possui conhecimento solido da linguagem Java (17+)?", "", "Selecionar opcao", true, false, false, "",
                ["Selecionar opcao", "Yes", "No"], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("java-combo", action.ElementId);
        Assert.Equal("No", action.Value);
    }

    [Fact]
    public async Task Decide_BotaoSelecionarOpcaoSemOptions_TrataComoSelect()
    {
        var profile = new CandidateProfile
        {
            Skills = ["Node.js", "Git", "GitHub"]
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "linkedin-dropdown", "button", "", "", "Voce possui conhecimento em Git e GitHub?* Selecionar opcao", "Selecionar opcao", "Selecionar opcao", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("linkedin-dropdown", action.ElementId);
        Assert.Equal("Sim", action.Value);
    }

    [Fact]
    public async Task Decide_ComSelectsInvalidos_NaoClicaRevisarAntesDeCorrigir()
    {
        var profile = new CandidateProfile
        {
            Skills = ["Node.js", "Git", "GitHub"],
            SalaryExpectation = "3800,00"
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn",
            "Insira uma resposta valida Insira um numero decimal com mais de 0.0", [
            new ElementCandidate(
                "node-dropdown", "button", "", "", "Voce possui experiencia pratica com Node.js?* Selecionar opcao", "Selecionar opcao", "Selecionar opcao", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32)),
            new ElementCandidate(
                "review", "button", "", "", "Revise sua candidatura Perguntas adicionais Voltar Revisar", "Voltar Revisar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("select", action.Type);
        Assert.Equal("node-dropdown", action.ElementId);
    }

    [Fact]
    public async Task Decide_SalarioDecimalExistente_CorrigeParaInteiro()
    {
        var profile = new CandidateProfile { SalaryExpectation = "3800,00" };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "salary1", "input", "text", "", "Qual e a sua pretensao salarial?", "", "3800,00", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32),
                InputMode: "numeric")
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("fill", action.Type);
        Assert.Equal("salary1", action.ElementId);
        Assert.Equal("3800", action.Value);
    }

    [Fact]
    public async Task Decide_CidadePreenchidaErrada_CorrigeComPerfil()
    {
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Suss",
            City = "Penápolis",
            Country = "Brazil"
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "city1", "input", "text", "", "Cidade atual", "", "São Paulo", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("fill", action.Type);
        Assert.Equal("city1", action.ElementId);
        Assert.Equal("Penápolis", action.Value);
    }

    [Fact]
    public async Task Decide_LastNamePreenchidoComSobrenomesCompletos_CorrigeParaUltimoSobrenome()
    {
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Sussai Silva"
        };
        var resolver = CreateResolver(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = CreateTask(profile);
        var observation = new PageObservation("https://www.linkedin.com/jobs/search/?currentJobId=1", "LinkedIn", "", [
            new ElementCandidate(
                "last1", "input", "text", "", "Last name", "", "Portie Sussai Silva", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32),
                AutoComplete: "family-name")
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("fill", action.Type);
        Assert.Equal("last1", action.ElementId);
        Assert.Equal("Silva", action.Value);
    }

    private static LocalAnswerResolver CreateResolver(CandidateProfile profile)
    {
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        resolver.SetProfile(profile);
        return resolver;
    }

    private static AgentTaskResponse CreateTask(CandidateProfile profile) =>
        new(new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
            new JobLead { Title = "Dev", Company = "Empresa", Url = "https://www.linkedin.com/jobs/view/1" },
            profile,
            []);
}
