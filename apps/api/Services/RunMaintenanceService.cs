using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationMaintenanceService(
    IRepository<AgentRun> runs,
    IRepository<ApplicationJob> jobs,
    IRepository<ExecutionEvent> events,
    IUnitOfWork unitOfWork)
{
    public async Task<ApplicationMaintenanceResult> RecoverStaleWorkAsync(DateTimeOffset now, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var staleRuns = await RecoverStaleAgentRunsAsync(now, userId, cancellationToken);
        var staleJobs = await RecoverStaleApplicationJobsAsync(now, userId, cancellationToken);
        if (staleRuns > 0 || staleJobs > 0)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);
        return new ApplicationMaintenanceResult(staleRuns, staleJobs);
    }

    private async Task<int> RecoverStaleAgentRunsAsync(DateTimeOffset now, Guid? userId, CancellationToken cancellationToken)
    {
        var cutoff = now.AddHours(-2);
        var query = runs.Query()
            .Where(x => x.State == "running" && x.StartedAt < cutoff)
            .AsQueryable();
        if (userId is not null)
            query = query.Where(x => x.UserId == userId);

        var staleRuns = await query.ToListAsync(cancellationToken);

        foreach (var run in staleRuns)
        {
            run.State = "failed";
            run.CompletedAt = now;
            events.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = run.UserId,
                ApplicationJobId = run.ApplicationJobId,
                EventType = "maintenance.agent_run_stale",
                Detail = $"Marked stale agent run as failed: run={run.Id}; startedAt={run.StartedAt:O}."
            });
        }

        return staleRuns.Count;
    }

    private async Task<int> RecoverStaleApplicationJobsAsync(DateTimeOffset now, Guid? userId, CancellationToken cancellationToken)
    {
        var cutoff = now.AddMinutes(-10);
        var query = jobs.Query()
            .Include(x => x.JobLead)
            .Where(x => x.State == JobStatuses.Opening || x.State == JobStatuses.Filling)
            .AsQueryable();
        if (userId is not null)
            query = query.Where(x => x.UserId == userId);

        var staleJobs = await query.ToListAsync(cancellationToken);

        var recovered = 0;
        foreach (var job in staleJobs)
        {
            var lastEventAt = await events.Query()
                .Where(x => x.UserId == job.UserId && x.ApplicationJobId == job.Id)
                .MaxAsync(x => (DateTimeOffset?)x.OccurredAt, cancellationToken) ?? job.CreatedAt;

            if (lastEventAt >= cutoff)
                continue;

            job.State = JobStatuses.NeedsReview;
            job.Reason = "agent_interrupted";
            if (job.JobLead is not null)
            {
                job.JobLead.Status = JobStatuses.NeedsReview;
                job.JobLead.AutomationSupport = "review_required";
                job.JobLead.AutomationReason = "agent_interrupted";
            }

            events.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = job.UserId,
                ApplicationJobId = job.Id,
                EventType = "maintenance.application_job_stale",
                Detail = $"Recovered stale application job after no activity since {lastEventAt:O}."
            });
            recovered++;
        }

        return recovered;
    }
}

public sealed record ApplicationMaintenanceResult(int StaleAgentRuns, int StaleApplicationJobs);

public sealed class RunMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<RunMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<ApplicationMaintenanceService>();
                var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
                var result = await maintenance.RecoverStaleWorkAsync(DateTimeOffset.UtcNow, cancellationToken: stoppingToken);
                var cleanup = await retention.CleanupExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (cleanup.AiUsageLogsRemoved > 0 || cleanup.ExportsRemoved > 0)
                {
                    logger.LogInformation(
                        "Retention cleanup removed logs and exports. AiUsageLogs={Logs}; Exports={Exports}; Files={Files}.",
                        cleanup.AiUsageLogsRemoved,
                        cleanup.ExportsRemoved,
                        cleanup.FilesRemoved);
                }
                if (result.StaleAgentRuns > 0 || result.StaleApplicationJobs > 0)
                {
                    logger.LogInformation(
                        "Recovered stale JobPilot work. AgentRuns={AgentRuns}; ApplicationJobs={ApplicationJobs}.",
                        result.StaleAgentRuns,
                        result.StaleApplicationJobs);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Maintenance loop failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
