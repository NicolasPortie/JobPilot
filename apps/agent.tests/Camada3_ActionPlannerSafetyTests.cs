using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Tests;

public class Camada3_ActionPlannerSafetyTests
{
    [Fact]
    public async Task Decide_CoverLetterObrigatoria_NaoAnexaCurriculo()
    {
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(resumePath, "fake resume");
        try
        {
            var profile = new CandidateProfile { ResumePath = resumePath };
            var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
            resolver.SetProfile(profile);
            var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
            var task = new AgentTaskResponse(
                new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
                new JobLead { Title = "Dev", Company = "Empresa", Url = "https://example.com/apply" },
                profile,
                []);
            var observation = new PageObservation("https://example.com/apply", "ATS", "", [
                new ElementCandidate(
                    "cover", "input", "file", "", "Cover letter", "", "", true, false, false, "",
                    [], new ElementBounds(0, 0, 200, 32))
            ]);

            var action = await engine.DecideAsync(observation, task, "job");

            Assert.NotNull(action);
            Assert.Equal("checkpoint", action.Type);
            Assert.Contains("not a resume", action.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task Decide_CurriculoObrigatorio_UsaResumePathConfigurado()
    {
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(resumePath, "fake resume");
        try
        {
            var profile = new CandidateProfile { ResumePath = resumePath };
            var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
            resolver.SetProfile(profile);
            var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
            var task = new AgentTaskResponse(
                new ApplicationJob(Guid.NewGuid(), "queued_to_apply"),
                new JobLead { Title = "Dev", Company = "Empresa", Url = "https://example.com/apply" },
                profile,
                []);
            var observation = new PageObservation("https://example.com/apply", "ATS", "", [
                new ElementCandidate(
                    "resume", "input", "file", "", "Curriculo", "", "", true, false, false, "",
                    [], new ElementBounds(0, 0, 200, 32))
            ]);

            var action = await engine.DecideAsync(observation, task, "job");

            Assert.NotNull(action);
            Assert.Equal("upload_file", action.Type);
            Assert.Equal(resumePath, action.Value);
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task Decide_CurriculoObrigatorio_PrefereResumeDaCandidatura()
    {
        var profileResumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-profile-{Guid.NewGuid():N}.pdf");
        var applicationResumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-application-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(profileResumePath, "generic resume");
        await File.WriteAllTextAsync(applicationResumePath, "tailored resume");
        try
        {
            var profile = new CandidateProfile { ResumePath = profileResumePath };
            var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
            resolver.SetProfile(profile);
            var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
            var task = new AgentTaskResponse(
                new ApplicationJob(Guid.NewGuid(), "queued_to_apply")
                {
                    ResumeVersionId = Guid.NewGuid(),
                    ResumeExportId = Guid.NewGuid(),
                    ResumeFilePath = applicationResumePath
                },
                new JobLead { Title = "Dev", Company = "Empresa", Url = "https://example.com/apply" },
                profile,
                []);
            var observation = new PageObservation("https://example.com/apply", "ATS", "", [
                new ElementCandidate(
                    "resume", "input", "file", "", "Curriculo", "", "", true, false, false, "",
                    [], new ElementBounds(0, 0, 200, 32))
            ]);

            var action = await engine.DecideAsync(observation, task, "job");

            Assert.NotNull(action);
            Assert.Equal("upload_file", action.Type);
            Assert.Equal(applicationResumePath, action.Value);
        }
        finally
        {
            if (File.Exists(profileResumePath)) File.Delete(profileResumePath);
            if (File.Exists(applicationResumePath)) File.Delete(applicationResumePath);
        }
    }

    [Fact]
    public async Task Decide_CurriculoDaCandidaturaAusente_NaoUsaFallbackDoPerfil()
    {
        var profileResumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-profile-{Guid.NewGuid():N}.pdf");
        var missingApplicationResumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-missing-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(profileResumePath, "generic resume");
        try
        {
            var profile = new CandidateProfile { ResumePath = profileResumePath };
            var resolver = new LocalAnswerResolver(new JobPilotApiClient("http://localhost:5000", "fake-token"));
            resolver.SetProfile(profile);
            var engine = new DecisionEngine(resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
            var task = new AgentTaskResponse(
                new ApplicationJob(Guid.NewGuid(), "queued_to_apply")
                {
                    ResumeVersionId = Guid.NewGuid(),
                    ResumeExportId = Guid.NewGuid(),
                    ResumeFilePath = missingApplicationResumePath
                },
                new JobLead { Title = "Dev", Company = "Empresa", Url = "https://example.com/apply" },
                profile,
                []);
            var observation = new PageObservation("https://example.com/apply", "ATS", "", [
                new ElementCandidate(
                    "resume", "input", "file", "", "Curriculo", "", "", true, false, false, "",
                    [], new ElementBounds(0, 0, 200, 32))
            ]);

            var action = await engine.DecideAsync(observation, task, "job");

            Assert.NotNull(action);
            Assert.Equal("checkpoint", action.Type);
            Assert.Contains(missingApplicationResumePath, action.Reason);
            Assert.DoesNotContain(profileResumePath, action.Reason);
        }
        finally
        {
            if (File.Exists(profileResumePath)) File.Delete(profileResumePath);
        }
    }
}
