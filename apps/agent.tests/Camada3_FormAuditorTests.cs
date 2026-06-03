using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Tests;

public class Camada3_FormAuditorTests
{
    [Fact]
    public void Audit_NaoBloqueiaCampoTextoOpcionalVazio()
    {
        var auditor = CreateAuditor();
        var task = CreateTask();
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "optional", "input", "text", "", "Website opcional", "", "", false, false, false, "",
                [], new ElementBounds(0, 0, 200, 32)),
            new ElementCandidate(
                "next", "button", "", "", "Avancar", "Avancar", "", false, false, false, "",
                [], new ElementBounds(0, 0, 100, 32))
        ], HasModal: true);

        var result = auditor.Audit(observation, task);

        Assert.DoesNotContain(result.Issues, issue => issue.Field.ElementId == "optional");
    }

    [Fact]
    public void Audit_BloqueiaCampoTextoObrigatorioVazio()
    {
        var auditor = CreateAuditor();
        var task = CreateTask();
        var observation = new PageObservation("https://www.linkedin.com/jobs/view/1", "LinkedIn", "", [
            new ElementCandidate(
                "required", "input", "text", "", "Telefone", "", "", true, false, false, "",
                [], new ElementBounds(0, 0, 200, 32))
        ], HasModal: true);

        var result = auditor.Audit(observation, task);

        Assert.Contains(result.Issues, issue => issue.Field.ElementId == "required");
    }

    private static FormAuditor CreateAuditor()
    {
        var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
        return new FormAuditor(new PageStateClassifier(), new FieldValuePolicy(resolver));
    }

    private static AgentTaskResponse CreateTask() =>
        new(new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
            new JobLead { Title = "Dev", Company = "Empresa", Url = "https://www.linkedin.com/jobs/view/1" },
            new CandidateProfile(),
            []);
}
