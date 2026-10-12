using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public sealed class AgentProtocolServiceTests
{
    [Fact]
    public async Task StartRunAsync_CreatesAndReturnsAgentRun()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.StartRunAsync(userId, new AgentRunRequest(null, "apply-agent", "auto", "conservative"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("running", result.State);
        Assert.Equal("apply-agent", result.AgentName);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
    }

    [Fact]
    public async Task CompleteRunAsync_WithExistingRun_ReturnsOk()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var run = new AgentRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgentName = "apply-agent",
            State = "running"
        };
        db.AgentRuns.Add(run);
        await db.SaveChangesAsync();

        var result = await service.CompleteRunAsync(userId, run.Id, new AgentRunCompleteRequest("completed"));

        Assert.Equal(ServiceResultStatus.Ok, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("completed", result.Value.State);
        Assert.NotNull(result.Value.CompletedAt);
    }

    [Fact]
    public async Task CompleteRunAsync_WithMissingRun_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.CompleteRunAsync(Guid.NewGuid(), Guid.NewGuid(), new AgentRunCompleteRequest("completed"));

        Assert.Equal(ServiceResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SaveStepAsync_CreatesAgentStep()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        await service.SaveStepAsync(userId, new AgentStepRequest(
            runId, null, 1, "apply-agent", "login_page", "click", "Submit", "ok", "", 0.95m
        ));

        var step = await db.AgentSteps.FirstAsync();
        Assert.Equal(userId, step.UserId);
        Assert.Equal(runId, step.AgentRunId);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal("click", step.ActionType);
    }

    [Fact]
    public async Task CreateCheckpointAsync_CreatesCheckpoint()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.CreateCheckpointAsync(userId, new AgentCheckpointRequest(
            null, null, null, "manual_review", "apply", "Salary", "Need confirmation", "Confirm value", "reobserve"
        ));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("open", result.ResolutionStatus);
        Assert.Equal("reobserve", result.ResumeMode);
        Assert.Equal(1, await db.Checkpoints.CountAsync());
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static AgentProtocolService CreateService(JobPilotDbContext db)
    {
        return TestServices.AgentProtocolService(db, new FakeAiChatService());
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        public Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile) =>
            Task.FromResult(new AnswerDraftResponse("", false, ""));

        public Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers) =>
            Task.FromResult(new BrowserActionPlanResponse("", "", "", "", 0m, false));

        public Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter) =>
            Task.FromResult(new JobFitReviewResponse("", 0, "", "", "", "", [], [], 0m));

        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            Task.FromResult<ResumeProfileImportData?>(null);

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input) =>
            Task.FromResult<JobRequirementExtractionResponse?>(null);

        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            Task.FromResult<TailoredResumeGenerationResponse?>(null);

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            Task.FromResult(new PageStateResponse("", "", 0m));

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            Task.FromResult(new StepValidationResponse(false, "", 0m, false));

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            Task.FromResult(new AnswerDraftResponse("", false, ""));
    }
}
