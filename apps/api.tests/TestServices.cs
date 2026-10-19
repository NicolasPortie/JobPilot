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

    public static JwtTokenService JwtTokenService(JobPilotDbContext db, IConfiguration configuration) =>
        new(configuration);

    public static CandidateProfileService CandidateProfileService(JobPilotDbContext db) =>
        new(Repo<CandidateProfile>(db), Repo<ProfileSkillRecommendation>(db), Repo<ProfileValidationRule>(db), Uow(db), ProfileGuard(db));

    public static ProfileGuard ProfileGuard(JobPilotDbContext db, ResumeTextService? resumeTextService = null) =>
        new(Repo<CandidateProfile>(db), Repo<ProfileValidationRule>(db), Uow(db), resumeTextService);

    public static ResumeEvidenceMapService ResumeEvidenceMapService(JobPilotDbContext db) =>
        new(Repo<ResumeVersion>(db), Repo<JobTarget>(db), Repo<CandidateProfile>(db), Repo<ResumeEvidenceMapRun>(db), Uow(db));

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
        new(AiCompletionService(http, configuration, db));

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
