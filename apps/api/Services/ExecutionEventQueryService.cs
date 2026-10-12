using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ExecutionEventQueryService(IRepository<ExecutionEvent> events)
{
    public async Task<List<ExecutionEventResponse>> ListRecentAsync(Guid userId, int limit = 100) =>
        (await events.Query()
            .Where(executionEvent => executionEvent.UserId == userId)
            .OrderByDescending(executionEvent => executionEvent.OccurredAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync())
        .Select(ExecutionEventResponse.From)
        .ToList();
}
