using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public sealed class AgentManagementServiceTests
{
    [Fact]
    public async Task SavePolicyAsync_UsesAuthenticatedUserAndNormalizesExecutionTarget()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var response = await service.SavePolicyAsync(
            userId,
            new AgentPolicyRequest("conservative", "playwright", 10, 40, [], [], false));

        Assert.Equal(ExecutionTargets.LocalDedicatedBrowser, response.ExecutionTarget);
        Assert.Equal(userId, (await db.AgentPolicies.SingleAsync()).UserId);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOnlyAuthenticatedUsersHistory()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentRuns.AddRange(
            new AgentRun { Id = Guid.NewGuid(), UserId = userId, AgentName = "Mine" },
            new AgentRun { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AgentName = "Another" });
        await db.SaveChangesAsync();

        var response = await CreateService(db).GetHistoryAsync(userId);

        Assert.Collection(response.Runs, run => Assert.Equal("Mine", run.AgentName));
    }

    private static AgentManagementService CreateService(JobPilotDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "development-only-jwt-key-that-is-long-enough"
            })
            .Build();
        return TestServices.AgentManagementService(db, new FakeAiChatService(), configuration);
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
        public Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile) =>
            Task.FromResult(new AnswerDraftResponse("", false, ""));

        public Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers) =>
            throw new NotSupportedException();

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            throw new NotSupportedException();

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            throw new NotSupportedException();

        public Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter) =>
            throw new NotSupportedException();

        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            throw new NotSupportedException();

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input) =>
            throw new NotSupportedException();

        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            throw new NotSupportedException();

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            throw new NotSupportedException();
    }
}
