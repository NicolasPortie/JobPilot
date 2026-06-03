using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationMaintenanceService
{
    private readonly JobPilotDbContext _db;

    public ApplicationMaintenanceService(JobPilotDbContext db)
    {
        _db = db;
    }

    public async Task<ApplicationMaintenanceResult> RecoverStaleWorkAsync(DateTimeOffset now, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var staleRuns = await RecoverStaleAgentRunsAsync(now, userId, cancellationToken);
        var staleJobs = await RecoverStaleApplicationJobsAsync(now, userId, cancellationToken);
        if (staleRuns > 0 || staleJobs > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return new ApplicationMaintenanceResult(staleRuns, staleJobs);
    }

    private async Task<int> RecoverStaleAgentRunsAsync(DateTimeOffset now, Guid? userId, CancellationToken cancellationToken)
    {
        var cutoff = now.AddHours(-2);
        var query = _db.AgentRuns
            .Where(x => x.State == "running" && x.StartedAt < cutoff)
            .AsQueryable();
        if (userId is not null)
            query = query.Where(x => x.UserId == userId);

        var runs = await query.ToListAsync(cancellationToken);

        foreach (var run in runs)
        {
            run.State = "failed";
            run.CompletedAt = now;
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = run.UserId,
                ApplicationJobId = run.ApplicationJobId,
                EventType = "maintenance.agent_run_stale",
                Detail = $"Marked stale agent run as failed: run={run.Id}; startedAt={run.StartedAt:O}."
            });
        }

        return runs.Count;
    }

    private async Task<int> RecoverStaleApplicationJobsAsync(DateTimeOffset now, Guid? userId, CancellationToken cancellationToken)
    {
        var cutoff = now.AddMinutes(-10);
        var query = _db.ApplicationJobs
            .Include(x => x.JobLead)
            .Where(x => x.State == JobStatuses.Opening || x.State == JobStatuses.Filling)
            .AsQueryable();
        if (userId is not null)
            query = query.Where(x => x.UserId == userId);

        var jobs = await query.ToListAsync(cancellationToken);

        var recovered = 0;
        foreach (var job in jobs)
        {
            var lastEventAt = await _db.ExecutionEvents
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

            _db.ExecutionEvents.Add(new ExecutionEvent
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
                var result = await maintenance.RecoverStaleWorkAsync(DateTimeOffset.UtcNow, cancellationToken: stoppingToken);
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
