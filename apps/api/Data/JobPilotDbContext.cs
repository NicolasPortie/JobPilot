using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Data;

public class JobPilotDbContext(DbContextOptions<JobPilotDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<ResumeImport> ResumeImports => Set<ResumeImport>();
    public DbSet<ResumeDocument> ResumeDocuments => Set<ResumeDocument>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
    public DbSet<ResumeSection> ResumeSections => Set<ResumeSection>();
    public DbSet<ResumeExport> ResumeExports => Set<ResumeExport>();
    public DbSet<ResumeTailoringRun> ResumeTailoringRuns => Set<ResumeTailoringRun>();
    public DbSet<ResumeReviewAnswer> ResumeReviewAnswers => Set<ResumeReviewAnswer>();
    public DbSet<JobTarget> JobTargets => Set<JobTarget>();
    public DbSet<JobRequirement> JobRequirements => Set<JobRequirement>();
    public DbSet<ResumeEvidenceMapRun> ResumeEvidenceMapRuns => Set<ResumeEvidenceMapRun>();
    public DbSet<ResumeRequirementEvidence> ResumeRequirementEvidences => Set<ResumeRequirementEvidence>();
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<ProfileValidationRule> ProfileValidationRules => Set<ProfileValidationRule>();
    public DbSet<ProfileSkillRecommendation> ProfileSkillRecommendations => Set<ProfileSkillRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<CandidateProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<ResumeImport>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<ResumeDocument>().HasIndex(x => new { x.UserId, x.UpdatedAt });
        modelBuilder.Entity<ResumeVersion>().HasIndex(x => new { x.UserId, x.ResumeDocumentId, x.UpdatedAt });
        modelBuilder.Entity<ResumeSection>().HasIndex(x => new { x.ResumeVersionId, x.SortOrder });
        modelBuilder.Entity<ResumeExport>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.CreatedAt });
        modelBuilder.Entity<ResumeTailoringRun>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.CreatedAt });
        modelBuilder.Entity<JobTarget>().HasIndex(x => new { x.UserId, x.UpdatedAt });
        modelBuilder.Entity<JobRequirement>().HasIndex(x => new { x.JobTargetId, x.SortOrder });
        modelBuilder.Entity<ResumeEvidenceMapRun>().HasIndex(x => new { x.UserId, x.ResumeVersionId, x.JobTargetId, x.CreatedAt });
        modelBuilder.Entity<ResumeRequirementEvidence>().HasIndex(x => new { x.ResumeEvidenceMapRunId, x.SortOrder });
        modelBuilder.Entity<ResumeDocument>()
            .HasMany(x => x.Versions)
            .WithOne(x => x.Document)
            .HasForeignKey(x => x.ResumeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ResumeVersion>()
            .HasMany(x => x.Sections)
            .WithOne(x => x.Version)
            .HasForeignKey(x => x.ResumeVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobTarget>()
            .HasMany(x => x.Requirements)
            .WithOne(x => x.JobTarget)
            .HasForeignKey(x => x.JobTargetId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ResumeEvidenceMapRun>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.ResumeEvidenceMapRunId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AiProvider>().HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        modelBuilder.Entity<AiUsageLog>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<AiUsageLog>().HasIndex(x => new { x.UserId, x.Operation, x.CreatedAt });
        modelBuilder.Entity<ProfileValidationRule>().HasIndex(x => new { x.UserId, x.FieldName }).IsUnique();
        modelBuilder.Entity<ProfileSkillRecommendation>().HasIndex(x => new { x.UserId, x.CreatedAt });
        modelBuilder.Entity<ResumeReviewAnswer>().HasIndex(x => new { x.UserId, x.JobRequirementId });
        modelBuilder.Entity<ResumeReviewAnswer>().HasIndex(x => new { x.UserId, x.ResumeEvidenceMapRunId });
    }
}
