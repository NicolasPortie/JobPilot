using System.Data.Common;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPilot.Api.Tests;

internal static class TestServices
{
    public static EfRepository<T> Repo<T>(JobPilotDbContext db) where T : class => new(db);
    public static IUnitOfWork Uow(JobPilotDbContext db) => new InMemoryUnitOfWork(db);

    public static AnswerResolver AnswerResolver(JobPilotDbContext db) =>
        new(Repo<QuestionPattern>(db), Uow(db));

    public static AnswerCatalogService AnswerCatalogService(JobPilotDbContext db) =>
        new(Repo<ReusableAnswer>(db), Repo<QuestionPattern>(db), Repo<CandidateProfile>(db), Uow(db), AnswerResolver(db));

    public static AgentAnswerResolutionService AgentAnswerResolutionService(JobPilotDbContext db, IAiChatService ai) =>
        new(Repo<CandidateProfile>(db), Repo<ApplicationAnswer>(db), Repo<Checkpoint>(db), Uow(db), AnswerResolver(db), ai);

    public static AgentLearningService AgentLearningService(JobPilotDbContext db) =>
        new(Repo<AgentLearning>(db), Repo<Checkpoint>(db), Uow(db));

    public static JwtTokenService JwtTokenService(JobPilotDbContext db, IConfiguration configuration) =>
        new(configuration, Repo<AgentDevice>(db), Uow(db));

    public static AgentManagementService AgentManagementService(JobPilotDbContext db, IAiChatService ai, IConfiguration configuration) =>
        new(
            Repo<AgentPolicy>(db),
            Repo<AgentDevice>(db),
            Repo<CandidateProfile>(db),
            Repo<AgentRun>(db),
            Repo<AgentStep>(db),
            Repo<ApplicationField>(db),
            Repo<ApplicationAnswer>(db),
            Repo<SiteWorkflowMemory>(db),
            Uow(db),
            ai,
            JwtTokenService(db, configuration));

    public static ApplicationAuditService ApplicationAuditService(JobPilotDbContext db) =>
        new(
            Repo<ApplicationJob>(db),
            Repo<ApplicationField>(db),
            Repo<ApplicationAnswer>(db),
            Repo<AgentStep>(db),
            Repo<AgentTraceStep>(db),
            Repo<Checkpoint>(db),
            Repo<ExecutionEvent>(db),
            Repo<AgentLearning>(db));

    public static AgentProtocolService AgentProtocolService(JobPilotDbContext db, IAiChatService ai) =>
        new(
            Repo<CandidateProfile>(db),
            Repo<ReusableAnswer>(db),
            Repo<AgentRun>(db),
            Repo<AgentStep>(db),
            Repo<AgentTraceStep>(db),
            Repo<ApplicationField>(db),
            Repo<ApplicationAnswer>(db),
            Repo<SiteWorkflowMemory>(db),
            Repo<ExecutionEvent>(db),
            Repo<Checkpoint>(db),
            Uow(db),
            ai,
            new TraceRedactionService());

    public static ApplicationMaintenanceService ApplicationMaintenanceService(JobPilotDbContext db) =>
        new(Repo<AgentRun>(db), Repo<ApplicationJob>(db), Repo<ExecutionEvent>(db), Uow(db));

    public static AutoApplyPolicyService AutoApplyPolicyService(JobPilotDbContext db) =>
        new(Repo<JobTarget>(db));

    public static CandidateProfileService CandidateProfileService(JobPilotDbContext db) =>
        new(Repo<CandidateProfile>(db), Repo<ProfileSkillRecommendation>(db), Repo<ProfileValidationRule>(db), Uow(db), ProfileGuard(db));

    public static ProfileGuard ProfileGuard(JobPilotDbContext db, ResumeTextService? resumeTextService = null) =>
        new(Repo<CandidateProfile>(db), Repo<ProfileValidationRule>(db), Uow(db), resumeTextService);

    public static JobFilterService JobFilterService(JobPilotDbContext db) =>
        new(Repo<JobFilter>(db), Uow(db));

    public static ApplicationLimitService ApplicationLimitService(JobPilotDbContext db) =>
        new(Repo<AgentPolicy>(db), Repo<ExecutionEvent>(db), Repo<ApplicationJob>(db));

    public static ResumeEvidenceMapService ResumeEvidenceMapService(JobPilotDbContext db) =>
        new(Repo<ResumeVersion>(db), Repo<JobTarget>(db), Repo<CandidateProfile>(db), Repo<ResumeEvidenceMapRun>(db), Uow(db));

    public static ApplicationResumeService ApplicationResumeService(JobPilotDbContext db) =>
        new(
            Repo<JobLead>(db),
            Repo<ApplicationRun>(db),
            Repo<ApplicationJob>(db),
            Repo<ResumeVersion>(db),
            Repo<ResumeExport>(db),
            Repo<ExecutionEvent>(db),
            Uow(db));

    public static ApplicationResumePreparationService ApplicationResumePreparationService(
        JobPilotDbContext db,
        IAiChatService ai,
        IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder().Build();
        var evidence = ResumeEvidenceMapService(db);
        return new(
            Repo<ApplicationJob>(db),
            Repo<ResumeVersion>(db),
            Repo<ExecutionEvent>(db),
            Uow(db),
            new JobTargetService(db, ai, new ResumeOcrService(), new JobTargetUrlExtractionService(new HttpClient())),
            new ResumeTailoringService(db, evidence, ai),
            new ResumeExportService(db, configuration),
            ApplicationResumeService(db));
    }

    public static ApplicationRunService ApplicationRunService(JobPilotDbContext db, IAiChatService ai, IConfiguration? configuration = null) =>
        new(
            Repo<CandidateProfile>(db),
            Repo<JobLead>(db),
            Repo<ApplicationRun>(db),
            Uow(db),
            JobFilterService(db),
            new ScoringService(),
            ApplicationResumePreparationService(db, ai, configuration));

    public static CheckpointResolutionService CheckpointResolutionService(JobPilotDbContext db) =>
        new(Repo<Checkpoint>(db), Uow(db), ReusableAnswerService(db));

    public static ReusableAnswerService ReusableAnswerService(JobPilotDbContext db) =>
        new(Repo<ReusableAnswer>(db));

    public static ResumeReviewAnswerService ResumeReviewAnswerService(JobPilotDbContext db, IAiChatService ai)
    {
        var evidence = ResumeEvidenceMapService(db);
        return new(
            Repo<ResumeReviewAnswer>(db),
            Repo<ResumeVersion>(db),
            Repo<JobTarget>(db),
            Repo<ResumeEvidenceMapRun>(db),
            Uow(db),
            evidence,
            new ResumeTailoringService(db, evidence, ai));
    }

    public static ResumeSectionService ResumeSectionService(JobPilotDbContext db) =>
        new(Repo<ResumeSection>(db), Repo<ResumeVersion>(db), Uow(db));

    public static AiChatService AiChatService(HttpClient http, IConfiguration configuration, JobPilotDbContext db) =>
        new(db, AnswerResolver(db), new ResumeTextService(), AiCompletionService(http, configuration, db));

    public static AiCompletionService AiCompletionService(HttpClient http, IConfiguration configuration, JobPilotDbContext db) =>
        new(http, configuration, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService());

    private sealed class InMemoryUnitOfWork(JobPilotDbContext db) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IDbContextTransaction>(new NoopTransaction());

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public DbTransaction GetDbTransaction() => throw new NotSupportedException("In-memory tests do not expose a DbTransaction.");
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
