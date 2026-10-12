using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Tests;

public class FieldValuePolicySafetyTests
{
    [Fact]
    public void ResolveExistingCorrection_NomeCompletoComHintTelefone_UsaNome()
    {
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Sussai Silva",
            Phone = "(18) 99664-6794"
        };
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        var policy = new FieldValuePolicy(resolver);
        var field = new ElementCandidate(
            "name",
            "input",
            "text",
            "",
            "Nome completo *",
            "",
            "(18) 99664-6794",
            true,
            false,
            false,
            "",
            [],
            new ElementBounds(0, 0, 200, 32),
            Placeholder: "Brasil +55",
            Name: "fullName");

        var correction = policy.ResolveExistingCorrection(field, profile);

        Assert.NotNull(correction);
        Assert.Equal(profile.FullName, correction.Value);
        Assert.Equal(SemanticFieldType.FullName, correction.InferredType);
    }

    [Fact]
    public void ResolveExistingCorrection_CelularComHintNomeCompleto_UsaTelefone()
    {
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Sussai Silva",
            Phone = "(18) 99664-6794"
        };
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        var policy = new FieldValuePolicy(resolver);
        var field = new ElementCandidate(
            "phone",
            "input",
            "tel",
            "",
            "Celular com DDD Nome completo *",
            "",
            "+55",
            true,
            false,
            false,
            "",
            [],
            new ElementBounds(0, 0, 200, 32),
            Placeholder: "Brasil +55",
            Name: "phone");

        var correction = policy.ResolveExistingCorrection(field, profile);

        Assert.NotNull(correction);
        Assert.Equal(profile.Phone, correction.Value);
        Assert.Equal(SemanticFieldType.Phone, correction.InferredType);
    }

    [Fact]
    public async Task ResolveText_LinkedinObrigatorioSemPerfil_NaoInventaComAi()
    {
        var profile = new CandidateProfile
        {
            FullName = "Nicolas Portie Sussai Silva",
            LinkedinUrl = ""
        };
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        resolver.SetProfile(profile);
        var policy = new FieldValuePolicy(resolver);
        var field = new ElementCandidate(
            "linkedin",
            "input",
            "text",
            "",
            "Linkedin *",
            "",
            "",
            true,
            false,
            false,
            "",
            [],
            new ElementBounds(0, 0, 200, 32),
            Placeholder: "https://linkedin.com/in/seu-perfil",
            Name: "linkedinUsername");

        var result = await policy.ResolveTextAsync(field, new AgentTaskResponse(
            new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
            new JobLead(),
            profile,
            []), "job", new SessionContext(), new AgentPolicy("aggressive", false, 10, 50, [], []));

        Assert.Null(result);
    }


    [Fact]
    public async Task Decide_SelectObrigatorioSemRespostaSegura_CriaCheckpoint()
    {
        var profile = new CandidateProfile
        {
            Skills = ["C#", "ASP.NET Core"]
        };
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        resolver.SetProfile(profile);
        var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
        var task = new AgentTaskResponse(
            new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
            new JobLead { Title = "Dev", Company = "Empresa", Url = "https://www.linkedin.com/jobs/view/1" },
            profile,
            []);
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "education", "select", "", "", "Escolha sua formacao academica", "", "", true, false, false, "",
                ["Selecionar opcao", "Ensino medio", "Superior completo"], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var action = await engine.DecideAsync(observation, task, "job");

        Assert.NotNull(action);
        Assert.Equal("checkpoint", action.Type);
        Assert.Contains("no matching option", action.Reason, StringComparison.OrdinalIgnoreCase);
    }
}

