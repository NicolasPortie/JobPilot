using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class ApplicationRunServiceTests
{
    [Fact]
    public async Task CreateAsync_PreparaCurriculoDocxParaCandidaturaEnfileirada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var baseVersionId = Guid.NewGuid();
        var jobLeadId = Guid.NewGuid();
        var exportRoot = Path.Combine(Path.GetTempPath(), "jobpilot-run-export-tests", Guid.NewGuid().ToString("N"));

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            Email = "nicolas@example.com",
            Phone = "11999999999",
            ProfessionalSummary = "Desenvolvedor .NET com experiencia em APIs REST, SQL e React.",
            Skills = ["C#", ".NET", "ASP.NET Core", "SQL", "React"]
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = baseVersionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            Status = "base",
            PlainText = """
            Nicolas Portie

            RESUMO PROFISSIONAL
            Desenvolvedor .NET com experiencia em ASP.NET Core, APIs REST, SQL e React.
            """
        });
        db.JobLeads.Add(new JobLead
        {
            Id = jobLeadId,
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Company = "Acme",
            Location = "Remoto",
            Url = "https://example.com/jobs/1",
            Status = JobStatuses.Scored,
            Score = 90,
            Description = """
            Buscamos pessoa desenvolvedora .NET para construir APIs REST com ASP.NET Core,
            SQL e integracoes web. Experiencia com React, Git e boas praticas de qualidade
            sera considerada diferencial para atuar em produto remoto.
            """
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, Config(exportRoot));
        await service.CreateAsync(userId, new CreateRunRequest("Run ATS", [jobLeadId]));

        var task = await db.ApplicationJobs.SingleAsync(x => x.UserId == userId && x.JobLeadId == jobLeadId);
        Assert.NotNull(task.ResumeVersionId);
        Assert.NotNull(task.ResumeExportId);
        Assert.EndsWith(".docx", task.ResumeFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(task.ResumeFilePath));

        var export = await db.ResumeExports.SingleAsync(x => x.Id == task.ResumeExportId);
        Assert.Equal("docx", export.Format);
        Assert.Equal(task.ResumeVersionId, export.ResumeVersionId);
        Assert.Contains("Desenvolvedor .NET", export.ParseText);
        Assert.Contains(await db.ExecutionEvents.Select(x => x.EventType).ToListAsync(), x => x == "application.resume_prepared");
    }

    private static ApplicationRunService CreateService(JobPilotDbContext db, IConfiguration config)
    {
        var ai = new FakeAiChatService();
        var preparation = new ApplicationResumePreparationService(
            db,
            new JobTargetService(db, ai, new HttpClient(), new ResumeOcrService()),
            new ResumeTailoringService(db, new ResumeEvidenceMapService(db), ai),
            new ResumeExportService(db, config),
            new ApplicationResumeService(db));
        return new ApplicationRunService(db, new JobFilterService(db), new ScoringService(), preparation);
    }

    private static IConfiguration Config(string exportRoot) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Candidate:ResumeExportDirectory"] = exportRoot
            })
            .Build();

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
            Task.FromResult(new AnswerDraftResponse("", false, "fake"));

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
