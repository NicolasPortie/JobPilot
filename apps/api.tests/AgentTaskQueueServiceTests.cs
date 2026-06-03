using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class AgentTaskQueueServiceTests
{
    [Fact]
    public async Task AutoQueueAsync_NaoReenfileiraVagaJaAplicada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var lead = new JobLead
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Company = "TechCorp",
            Url = "https://www.linkedin.com/jobs/view/123",
            Status = JobStatuses.Scored,
            Score = 80
        };
        db.JobLeads.Add(lead);
        db.ApplicationRuns.Add(new ApplicationRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "previous",
            TotalJobs = 1,
            Jobs =
            [
                new ApplicationJob
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    JobLeadId = lead.Id,
                    JobUrl = lead.Url,
                    State = JobStatuses.Applied
                }
            ]
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.AutoQueueAsync(userId);

        Assert.Single(await db.ApplicationJobs.Where(x => x.UserId == userId && x.JobLeadId == lead.Id).ToListAsync());
        Assert.Empty(await db.ApplicationRuns.Where(x => x.UserId == userId && x.Name.StartsWith("Auto-queue")).ToListAsync());
    }

    [Fact]
    public async Task AutoQueueAsync_EnfileiraSomentePlataformasSuportadas()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.JobLeads.AddRange(
            Lead(userId, "https://www.linkedin.com/jobs/view/1", "linkedin_easy_apply"),
            Lead(userId, "https://empresa.inhire.app/jobs/2", "inhire"),
            Lead(userId, "https://example.com/apply/3", "unsupported_external"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.AutoQueueAsync(userId);

        var queued = await db.JobLeads.Where(x => x.UserId == userId && x.Status == JobStatuses.QueuedToApply).ToListAsync();
        var manual = await db.JobLeads.SingleAsync(x => x.UserId == userId && x.ApplyPlatform == "unsupported_external");
        Assert.Equal(2, queued.Count);
        Assert.Equal(JobStatuses.ManualOnly, manual.Status);
        Assert.Equal("manual_only", manual.AutomationSupport);
    }

    [Fact]
    public async Task AutoQueueAsync_PreparaCurriculoDocxParaCandidaturaEnfileirada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportRoot = Path.Combine(Path.GetTempPath(), "jobpilot-autoqueue-export-tests", Guid.NewGuid().ToString("N"));

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            ProfessionalSummary = "Desenvolvedor .NET com experiencia em APIs REST, SQL e React.",
            Skills = ["C#", ".NET", "ASP.NET Core", "SQL", "React"]
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = """
            Nicolas Portie

            Desenvolvedor .NET com experiencia em ASP.NET Core, APIs REST, SQL e React.
            """
        });
        var lead = Lead(userId, "https://www.linkedin.com/jobs/view/42", "linkedin_easy_apply");
        lead.Description = """
        Vaga para desenvolvimento .NET com ASP.NET Core, construcao de APIs REST,
        SQL, Git e integracoes web. React sera um diferencial para atuar em produto remoto.
        """;
        db.JobLeads.Add(lead);
        await db.SaveChangesAsync();
        var service = CreateService(db, exportRoot);

        await service.AutoQueueAsync(userId);

        var task = await db.ApplicationJobs.SingleAsync(x => x.UserId == userId && x.JobLeadId == lead.Id);
        Assert.NotNull(task.ResumeVersionId);
        Assert.NotNull(task.ResumeExportId);
        Assert.EndsWith(".docx", task.ResumeFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(task.ResumeFilePath));
        Assert.Contains(await db.ExecutionEvents.Select(x => x.EventType).ToListAsync(), x => x == "application.resume_prepared");
    }

    [Fact]
    public async Task AutoQueueAsync_RevalidaFiltrosAntesDeEnfileirar()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.JobFilters.Add(new JobFilter
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AllowedSeniorities = ["junior"],
            BlockedSeniorities = ["senior"],
            AllowedRoleKeywords = [".net"],
            BlockedRoleKeywords = ["suporte"],
            AllowedWorkplaceTypes = ["remote"],
            BlockedWorkplaceTypes = ["hybrid"],
            AllowedApplyTypes = ["linkedin_easy_apply"],
            MinScore = 80,
            AutoIgnoreLowScore = true
        });
        var lead = Lead(userId, "https://www.linkedin.com/jobs/view/14", "linkedin_easy_apply");
        lead.Score = 20;
        db.JobLeads.Add(lead);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.AutoQueueAsync(userId);

        var saved = await db.JobLeads.SingleAsync(x => x.UserId == userId);
        Assert.Equal(JobStatuses.Ignored, saved.Status);
        Assert.Contains("low_score", saved.FilteredReason);
        Assert.Empty(await db.ApplicationJobs.Where(x => x.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task AutoQueueAsync_RespeitaLimiteDiarioRestante()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId, MaxApplicationsPerDay = 2 });
        var alreadyAppliedTaskId = Guid.NewGuid();
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = alreadyAppliedTaskId,
            EventType = "job.applied",
            OccurredAt = DateTimeOffset.UtcNow
        });
        db.JobLeads.AddRange(
            Lead(userId, "https://www.linkedin.com/jobs/view/10", "linkedin_easy_apply"),
            Lead(userId, "https://www.linkedin.com/jobs/view/11", "linkedin_easy_apply"),
            Lead(userId, "https://www.linkedin.com/jobs/view/12", "linkedin_easy_apply"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.AutoQueueAsync(userId);

        var queued = await db.ApplicationJobs.Where(x => x.UserId == userId && x.State == JobStatuses.QueuedToApply).ToListAsync();
        Assert.Single(queued);
    }

    [Fact]
    public async Task AutoQueueAsync_NaoEnfileiraQuandoPoliticaEstaPausada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId, IsPaused = true });
        db.JobLeads.Add(Lead(userId, "https://www.linkedin.com/jobs/view/13", "linkedin_easy_apply"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.AutoQueueAsync(userId);

        Assert.Contains("Ok", result.GetType().Name);
        var value = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IValueHttpResult>(result).Value;
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        Assert.Contains("paused", json);
        Assert.Empty(await db.ApplicationJobs.Where(x => x.UserId == userId).ToListAsync());
        Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "agent.policy.paused");
    }

    [Fact]
    public async Task GetNextTaskAsync_NaoEntregaTarefaQuandoLimiteDiarioAtingido()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId, MaxApplicationsPerDay = 1 });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = Guid.NewGuid(),
            EventType = "job.applied",
            OccurredAt = DateTimeOffset.UtcNow
        });
        var lead = Lead(userId, "https://www.linkedin.com/jobs/view/20", "linkedin_easy_apply");
        lead.Status = JobStatuses.QueuedToApply;
        db.JobLeads.Add(lead);
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobLeadId = lead.Id,
            JobUrl = lead.Url,
            State = JobStatuses.QueuedToApply
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetNextTaskAsync(userId);

        Assert.Contains("NoContent", result.GetType().Name);
        Assert.Equal(JobStatuses.QueuedToApply, (await db.ApplicationJobs.SingleAsync(x => x.UserId == userId && x.JobLeadId == lead.Id)).State);
        Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "agent.daily_limit_reached");
    }

    [Fact]
    public async Task GetNextTaskAsync_NaoEntregaTarefaQuandoPoliticaEstaPausada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId, IsPaused = true });
        var lead = Lead(userId, "https://www.linkedin.com/jobs/view/21", "linkedin_easy_apply");
        lead.Status = JobStatuses.QueuedToApply;
        db.JobLeads.Add(lead);
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobLeadId = lead.Id,
            JobUrl = lead.Url,
            State = JobStatuses.QueuedToApply
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetNextTaskAsync(userId);

        Assert.Contains("NoContent", result.GetType().Name);
        var task = await db.ApplicationJobs.SingleAsync(x => x.UserId == userId && x.JobLeadId == lead.Id);
        Assert.Equal(JobStatuses.QueuedToApply, task.State);
        Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "agent.policy.paused");
    }

    [Fact]
    public async Task UpdateStateAsync_GravaEventoAplicadoAoMarcarApplied()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var lead = Lead(userId, "https://www.linkedin.com/jobs/view/30", "linkedin_easy_apply");
        lead.Status = JobStatuses.Opening;
        var taskId = Guid.NewGuid();
        db.JobLeads.Add(lead);
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = taskId,
            UserId = userId,
            JobLeadId = lead.Id,
            JobUrl = lead.Url,
            State = JobStatuses.Opening
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.UpdateStateAsync(userId, taskId, new AgentStateRequest(JobStatuses.Applied, "confirmation_detected"));
        await service.UpdateStateAsync(userId, taskId, new AgentStateRequest(JobStatuses.Applied, "confirmation_detected"));

        var events = await db.ExecutionEvents.Where(x => x.UserId == userId && x.ApplicationJobId == taskId && x.EventType == "job.applied").ToListAsync();
        Assert.Single(events);
        Assert.Equal("confirmation_detected", events[0].Detail);
    }

    [Fact]
    public async Task SaveDiscoveredJobsAsync_NaoSobrescreveVagaProtegida()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var lead = new JobLead
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Desenvolvedor junior",
            Company = "Eleja AI",
            Url = "https://www.linkedin.com/jobs/view/4418354112/",
            Status = JobStatuses.Applied,
            Score = 20,
            ScoreReason = "original"
        };
        db.JobLeads.Add(lead);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SaveDiscoveredJobsAsync(userId, new DiscoveredJobsRequest([
            new DiscoveredJobDto("Volte a experimentar o Premium", "", "", "", lead.Url, false)
        ]));

        var saved = await db.JobLeads.SingleAsync(x => x.Id == lead.Id);
        Assert.Equal("Desenvolvedor junior", saved.Title);
        Assert.Equal("Eleja AI", saved.Company);
        Assert.Equal(JobStatuses.Applied, saved.Status);
        Assert.Equal(20, saved.Score);
        Assert.Equal("original", saved.ScoreReason);
    }

    [Fact]
    public async Task SaveDiscoveredJobsAsync_IgnoraCardPremium()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        await service.SaveDiscoveredJobsAsync(userId, new DiscoveredJobsRequest([
            new DiscoveredJobDto("Volte a experimentar o Premium", "", "", "", "https://www.linkedin.com/jobs/view/999/", false)
        ]));

        Assert.Empty(await db.JobLeads.Where(x => x.UserId == userId).ToListAsync());
    }

    private static JobLead Lead(Guid userId, string url, string applyPlatform) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = "Desenvolvedor .NET",
        Company = "TechCorp",
        Url = url,
        ApplyPlatform = applyPlatform,
        Status = JobStatuses.Scored,
        Score = 80,
        IsRemote = true
    };

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static AgentTaskQueueService CreateService(JobPilotDbContext db, string? exportRoot = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SearchHarness:Enabled"] = "false",
                ["Candidate:ResumeExportDirectory"] = exportRoot
            })
            .Build();
        var filterService = new JobFilterService(db);
        var ai = new FakeAiChatService();
        var preparation = new ApplicationResumePreparationService(
            db,
            new JobTargetService(db, ai, new HttpClient(), new ResumeOcrService()),
            new ResumeTailoringService(db, new ResumeEvidenceMapService(db), ai),
            new ResumeExportService(db, config),
            new ApplicationResumeService(db));
        return new AgentTaskQueueService(db, filterService, new ScoringService(), ai, config, new ApplicationLimitService(db), preparation);
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
