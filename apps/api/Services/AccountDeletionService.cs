using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPilot.Api.Services;

public sealed class AccountDeletionService(
    JobPilotDbContext db,
    IConfiguration configuration,
    ILogger<AccountDeletionService> logger)
{
    public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return false;
        db.CandidateProfiles.Remove(profile);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return false;

        var exportPaths = await db.ResumeExports.Where(x => x.UserId == userId).Select(x => x.FilePath).ToListAsync(cancellationToken);
        var importPaths = await db.ResumeImports.Where(x => x.UserId == userId).Select(x => x.SourcePath).ToListAsync(cancellationToken);
        var profilePaths = await db.CandidateProfiles.Where(x => x.UserId == userId).Select(x => x.ResumePath).ToListAsync(cancellationToken);

        await RemoveWhereAsync(db.Checkpoints.Where(x => x.UserId == userId), db.Checkpoints, cancellationToken);
        await RemoveWhereAsync(db.ExecutionEvents.Where(x => x.UserId == userId), db.ExecutionEvents, cancellationToken);
        await RemoveWhereAsync(db.AgentTraceSteps.Where(x => x.UserId == userId), db.AgentTraceSteps, cancellationToken);
        await RemoveWhereAsync(db.AgentSteps.Where(x => x.UserId == userId), db.AgentSteps, cancellationToken);
        await RemoveWhereAsync(db.AgentRuns.Where(x => x.UserId == userId), db.AgentRuns, cancellationToken);
        await RemoveWhereAsync(db.ApplicationAnswers.Where(x => x.UserId == userId), db.ApplicationAnswers, cancellationToken);
        await RemoveWhereAsync(db.ApplicationFields.Where(x => x.UserId == userId), db.ApplicationFields, cancellationToken);
        await RemoveWhereAsync(db.ApplicationJobs.Where(x => x.UserId == userId), db.ApplicationJobs, cancellationToken);
        await RemoveWhereAsync(db.ApplicationRuns.Where(x => x.UserId == userId), db.ApplicationRuns, cancellationToken);
        await RemoveWhereAsync(db.ResumeReviewAnswers.Where(x => x.UserId == userId), db.ResumeReviewAnswers, cancellationToken);
        await RemoveWhereAsync(db.ResumeRequirementEvidences.Where(x => x.UserId == userId), db.ResumeRequirementEvidences, cancellationToken);
        await RemoveWhereAsync(db.ResumeEvidenceMapRuns.Where(x => x.UserId == userId), db.ResumeEvidenceMapRuns, cancellationToken);
        await RemoveWhereAsync(db.ResumeTailoringRuns.Where(x => x.UserId == userId), db.ResumeTailoringRuns, cancellationToken);
        await RemoveWhereAsync(db.ResumeExports.Where(x => x.UserId == userId), db.ResumeExports, cancellationToken);
        await RemoveWhereAsync(db.ResumeSections.Where(x => x.UserId == userId), db.ResumeSections, cancellationToken);
        await RemoveWhereAsync(db.ResumeVersions.Where(x => x.UserId == userId), db.ResumeVersions, cancellationToken);
        await RemoveWhereAsync(db.ResumeDocuments.Where(x => x.UserId == userId), db.ResumeDocuments, cancellationToken);
        await RemoveWhereAsync(db.ResumeImports.Where(x => x.UserId == userId), db.ResumeImports, cancellationToken);
        await RemoveWhereAsync(db.JobRequirements.Where(x => x.UserId == userId), db.JobRequirements, cancellationToken);
        await RemoveWhereAsync(db.JobTargets.Where(x => x.UserId == userId), db.JobTargets, cancellationToken);
        await RemoveWhereAsync(db.JobLeads.Where(x => x.UserId == userId), db.JobLeads, cancellationToken);
        await RemoveWhereAsync(db.ReusableAnswers.Where(x => x.UserId == userId), db.ReusableAnswers, cancellationToken);
        await RemoveWhereAsync(db.QuestionPatterns.Where(x => x.UserId == userId), db.QuestionPatterns, cancellationToken);
        await RemoveWhereAsync(db.JobFilters.Where(x => x.UserId == userId), db.JobFilters, cancellationToken);
        await RemoveWhereAsync(db.ProfileValidationRules.Where(x => x.UserId == userId), db.ProfileValidationRules, cancellationToken);
        await RemoveWhereAsync(db.PlatformCredentials.Where(x => x.UserId == userId), db.PlatformCredentials, cancellationToken);
        await RemoveWhereAsync(db.AgentPolicies.Where(x => x.UserId == userId), db.AgentPolicies, cancellationToken);
        await RemoveWhereAsync(db.AgentDevices.Where(x => x.UserId == userId), db.AgentDevices, cancellationToken);
        await RemoveWhereAsync(db.AgentLearnings.Where(x => x.UserId == userId), db.AgentLearnings, cancellationToken);
        await RemoveWhereAsync(db.SiteWorkflowMemories.Where(x => x.UserId == userId), db.SiteWorkflowMemories, cancellationToken);
        await RemoveWhereAsync(db.AiUsageLogs.Where(x => x.UserId == userId), db.AiUsageLogs, cancellationToken);
        await RemoveWhereAsync(db.AiProviders.Where(x => x.UserId == userId), db.AiProviders, cancellationToken);
        await RemoveWhereAsync(db.CandidateProfiles.Where(x => x.UserId == userId), db.CandidateProfiles, cancellationToken);
        await RemoveWhereAsync(db.RefreshTokens.Where(x => x.UserId == userId), db.RefreshTokens, cancellationToken);

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var path in exportPaths.Concat(importPaths).Concat(profilePaths))
            TryDeleteFile(path);
        DeleteUserDirectory("Candidate:ResumeUploadDirectory", Path.Combine("documents", "resumes"), userId);
        DeleteUserDirectory("Candidate:ResumeExportDirectory", Path.Combine("documents", "resume-exports"), userId);
        DeleteUserDirectory("JobTargets:SourceDirectory", Path.Combine("storage", "job-target-sources"), userId);

        return true;
    }

    private static async Task RemoveWhereAsync<T>(IQueryable<T> query, DbSet<T> set, CancellationToken cancellationToken) where T : class
    {
        var items = await query.ToListAsync(cancellationToken);
        if (items.Count > 0) set.RemoveRange(items);
    }

    private void TryDeleteFile(string? path)
    {
        try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Account deletion could not remove a file. Database record was already removed."); }
    }

    private void DeleteUserDirectory(string configKey, string fallbackRelative, Guid userId)
    {
        var dir = string.Empty;
        try
        {
            var root = configuration[configKey];
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Directory.GetCurrentDirectory(), fallbackRelative);
            dir = Path.Combine(root, userId.ToString("N"));
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Account deletion could not remove user directory {Directory}.", dir); }
    }
}
