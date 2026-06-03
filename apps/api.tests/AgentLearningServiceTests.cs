using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class AgentLearningServiceTests
{
    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    [Fact]
    public async Task RecordOutcomeAsync_AutoApprovesTechnicalLearningAfterTwoSuccesses()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        var learning = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "combobox-select", "Combobox LinkedIn", "Custom dropdown handled.", "low"));

        Assert.Equal(AgentLearningStatuses.Candidate, learning.Status);

        await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "first replay ok"));
        var promoted = await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "second replay ok"));

        Assert.NotNull(promoted);
        Assert.Equal(AgentLearningStatuses.AutoApproved, promoted!.Status);
        Assert.Equal(2, promoted.SuccessCount);
    }

    [Fact]
    public async Task RecordOutcomeAsync_DoesNotAutoApproveSemanticAnswer()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        var learning = await service.UpsertCandidateAsync(userId, Request("answer", "linkedin.com", "node-years", "Resposta sobre anos de experiencia", "Responder 2 anos para Node.js.", "low"));

        await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "ok"));
        var result = await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "ok"));

        Assert.NotNull(result);
        Assert.Equal(AgentLearningStatuses.Candidate, result!.Status);
        Assert.Equal(AgentLearningTypes.Answer, result.LearningType);
        Assert.Equal(AgentLearningRiskLevels.Medium, result.RiskLevel);
    }

    [Fact]
    public async Task RecordOutcomeAsync_DoesNotAutoApproveGenericClickLearning()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        var learning = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "click-botao", "Clique em botao", "Clique generico validado.", "low"));

        await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "ok"));
        var result = await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, true, null, "ok"));

        Assert.NotNull(result);
        Assert.Equal(AgentLearningStatuses.Candidate, result!.Status);
        Assert.Equal(2, result.SuccessCount);
    }

    [Fact]
    public async Task UpsertCandidateAsync_ClassifiesSensitiveFieldsAsHighRisk()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        var learning = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "pcd-question", "Campo PCD", "Pergunta sobre deficiencia.", "low"));

        Assert.Equal(AgentLearningStatuses.Candidate, learning.Status);
        Assert.Equal(AgentLearningTypes.Sensitive, learning.LearningType);
        Assert.Equal(AgentLearningRiskLevels.High, learning.RiskLevel);
    }

    [Fact]
    public async Task UpsertCandidateAsync_DoesNotClassifyReviewTextAsVisaSensitive()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        var learning = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "review-button", "Botao Revisar", "Controle Revisar antes de enviar candidatura.", "low"));

        Assert.Equal(AgentLearningTypes.Technical, learning.LearningType);
        Assert.Equal(AgentLearningRiskLevels.Low, learning.RiskLevel);
    }

    [Fact]
    public async Task ActiveAsync_ReturnsApprovedOnlyAndIgnoresCandidates()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);

        await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "candidate", "Candidato", "Ainda nao validado.", "low"));
        var approved = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "approved", "Aprovado", "Validado por humano.", "low"));
        await service.ReviewAsync(userId, userId, approved.Id, "approve");

        var active = await service.ActiveAsync(userId);

        Assert.Single(active);
        Assert.Equal("linkedin-com-technical-approved", active[0].PatternKey);
        Assert.Equal(AgentLearningStatuses.Approved, active[0].Status);
    }

    [Fact]
    public async Task RecordOutcomeAsync_DisablesApprovedLearningAfterTwoFailures()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);
        var learning = await service.UpsertCandidateAsync(userId, Request("technical", "linkedin.com", "avoid-back", "Evitar Voltar", "Botao Voltar nao deve ser usado para enviar.", "low"));
        await service.ReviewAsync(userId, userId, learning.Id, "approve");

        await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, false, null, "failed once"));
        var disabled = await service.RecordOutcomeAsync(userId, new AgentLearningOutcomeRequest(learning.PatternKey, false, null, "failed twice"));

        Assert.NotNull(disabled);
        Assert.Equal(AgentLearningStatuses.Disabled, disabled!.Status);
        Assert.Equal(2, disabled.FailureCount);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusTypeRiskAndNormalizedDomain()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new AgentLearningService(db);
        await service.UpsertCandidateAsync(userId, Request("format", "https://www.linkedin.com/jobs/view/1", "salary-integer", "Salario inteiro", "Usar 3800 em vez de 3800,00.", "low"));
        await service.UpsertCandidateAsync(userId, Request("workflow", "gupy.io", "workflow-stuck", "Fluxo preso", "Navegacao travou.", "medium"));

        var filtered = await service.ListAsync(userId, AgentLearningStatuses.Candidate, AgentLearningTypes.Format, AgentLearningRiskLevels.Low, "linkedin.com");

        Assert.Single(filtered);
        Assert.Equal("linkedin.com", filtered[0].Domain);
        Assert.Contains("salary-integer", filtered[0].PatternKey);
    }

    private static AgentLearningRequest Request(string type, string domain, string key, string title, string summary, string risk) =>
        new(null, domain, type, key, title, summary, """{"field":"test"}""", """{"action":"suggest"}""", """{"source":"test"}""", risk);
}
