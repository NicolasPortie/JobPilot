using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class AgentAnswerResolutionServiceTests
{
    [Fact]
    public async Task ResolveAsync_PerguntaNumericaSemFonte_NaoInventaResposta()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas",
            Skills = ["C#", ".NET", "SQL"]
        });
        await db.SaveChangesAsync();
        var ai = new FakeAiChatService();
        var service = new AgentAnswerResolutionService(db, new AnswerResolver(db), ai);
        var request = new ResolveAgentAnswerRequest(
            Guid.NewGuid(),
            "Quantos anos voce trabalha com Java?",
            "Campo numerico obrigatorio",
            "Vaga Java",
            "number");

        var result = await service.ResolveAsync(userId, request);

        Assert.Equal("", result.AnswerValue);
        Assert.Equal("manual-required", result.Source);
        Assert.True(result.RequiresCheckpoint);
        Assert.False(ai.DraftWasCalled);
        Assert.Contains(await db.Checkpoints.ToListAsync(), checkpoint =>
            checkpoint.UserId == userId &&
            checkpoint.ReasonCode == "manual-required" &&
            checkpoint.FieldLabel == request.QuestionText);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        public bool DraftWasCalled { get; private set; }

        public Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile)
        {
            DraftWasCalled = true;
            return Task.FromResult(new AnswerDraftResponse("2", true, "fake"));
        }

        public Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers) =>
            Task.FromResult(new BrowserActionPlanResponse("checkpoint", "", "", "fake", 0m, true));

        public Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter) =>
            Task.FromResult(SearchDecisionHarness.UnavailableReview("fake"));

        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            Task.FromResult<ResumeProfileImportData?>(null);

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input) =>
            Task.FromResult<JobRequirementExtractionResponse?>(null);

        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            Task.FromResult<TailoredResumeGenerationResponse?>(null);

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            Task.FromResult(new PageStateResponse("unknown", "fake", 0m));

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            Task.FromResult(new StepValidationResponse(false, "fake", 0m, true));

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            Task.FromResult(new AnswerDraftResponse("fake", false, "fake"));
    }
}
