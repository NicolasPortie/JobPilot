using JobPilot.Api.Application;
using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPilot.Api.Services;

public sealed class RetentionService(JobPilotDbContext db, IConfiguration configuration, ILogger<RetentionService> logger)
{
    public RetentionPolicyResponse GetPolicy()
    {
        var source = RetentionPolicy.From(configuration);
        return new RetentionPolicyResponse(
            source.ResumeOriginalsDays,
            source.ResumeExportsDays,
            source.AiUsageLogsDays,
            source.JobSourceFilesDays);
    }

    public async Task<RetentionCleanupResult> CleanupExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var policy = GetPolicy();
        var logsCutoff = now.AddDays(-policy.AiUsageLogsDays);
        var exportsCutoff = now.AddDays(-policy.ResumeExportsDays);
        var originalsCutoff = now.AddDays(-policy.ResumeOriginalsDays);

        var oldLogs = await db.AiUsageLogs.Where(x => x.CreatedAt < logsCutoff).ToListAsync(cancellationToken);
        if (oldLogs.Count > 0) db.AiUsageLogs.RemoveRange(oldLogs);

        var oldExports = await db.ResumeExports.Where(x => x.CreatedAt < exportsCutoff).ToListAsync(cancellationToken);
        var removedFiles = 0;
        foreach (var export in oldExports)
        {
            if (TryDeleteFile(export.FilePath)) removedFiles++;
        }
        if (oldExports.Count > 0) db.ResumeExports.RemoveRange(oldExports);

        var oldImports = await db.ResumeImports.Where(x => x.CreatedAt < originalsCutoff).ToListAsync(cancellationToken);
        foreach (var import in oldImports)
        {
            if (TryDeleteFile(import.SourcePath)) removedFiles++;
        }
        if (oldImports.Count > 0) db.ResumeImports.RemoveRange(oldImports);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Retention cleanup finished. AiUsageLogs={Logs}; Exports={Exports}; Imports={Imports}; Files={Files}.",
            oldLogs.Count, oldExports.Count, oldImports.Count, removedFiles);
        return new RetentionCleanupResult(oldLogs.Count, oldExports.Count + oldImports.Count, removedFiles);
    }

    private bool TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) { File.Delete(path); return true; }
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Retention cleanup could not delete file.");
            return false;
        }
    }
}

internal sealed record RetentionPolicy(
    int ResumeOriginalsDays,
    int ResumeExportsDays,
    int AiUsageLogsDays,
    int JobSourceFilesDays)
{
    public static RetentionPolicy From(IConfiguration configuration) => new(
        GetDays(configuration, "Retention:ResumeOriginalsDays", 90),
        GetDays(configuration, "Retention:ResumeExportsDays", 180),
        GetDays(configuration, "Retention:AiUsageLogsDays", 90),
        GetDays(configuration, "Retention:JobSourceFilesDays", 30));

    private static int GetDays(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var days) && days > 0 ? days : fallback;
    }
}

public sealed record RetentionCleanupResult(int AiUsageLogsRemoved, int ExportsRemoved, int FilesRemoved);
