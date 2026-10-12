using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationLimitService(
    IRepository<AgentPolicy> policies,
    IRepository<ExecutionEvent> events,
    IRepository<ApplicationJob> jobs)
{
    public async Task<ApplicationLimitSnapshot> GetDailySnapshotAsync(Guid userId, AgentPolicy? policy = null)
    {
        policy ??= await policies.FirstOrDefaultAsync(x => x.UserId == userId);
        var dailyLimit = policy?.MaxApplicationsPerDay ?? 50;
        if (dailyLimit <= 0)
            return new ApplicationLimitSnapshot(dailyLimit, 0, int.MaxValue, false);

        var startOfDay = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var eventAppliedIds = await events.Query()
            .Where(x =>
                x.UserId == userId &&
                x.ApplicationJobId != null &&
                x.EventType == "job.applied" &&
                x.OccurredAt >= startOfDay)
            .Select(x => x.ApplicationJobId!.Value)
            .ToListAsync();

        var taskAppliedIds = await jobs.Query()
            .Where(x =>
                x.UserId == userId &&
                x.State == JobStatuses.Applied &&
                x.CreatedAt >= startOfDay)
            .Select(x => x.Id)
            .ToListAsync();

        var appliedToday = eventAppliedIds.Concat(taskAppliedIds).Distinct().Count();
        var remaining = Math.Max(0, dailyLimit - appliedToday);
        return new ApplicationLimitSnapshot(dailyLimit, appliedToday, remaining, remaining <= 0);
    }
}

public sealed record ApplicationLimitSnapshot(int DailyLimit, int AppliedToday, int RemainingToday, bool IsReached);
