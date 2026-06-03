using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationAuditService
{
    private readonly JobPilotDbContext _db;

    public ApplicationAuditService(JobPilotDbContext db) => _db = db;

    public async Task<Guid?> FindApplicationJobIdByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJob = await _db.ApplicationJobs.FirstOrDefaultAsync(x => x.JobLeadId == jobLeadId && x.UserId == userId);
        return appJob?.Id;
    }

    public async Task<IResult> GetFieldsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetFieldsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetAnswersByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetAnswersAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetStepsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetStepsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetCheckpointsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetCheckpointsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetEventsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetEventsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetLearningsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetLearningsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetTraceStepsByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null) return Results.Ok(new List<object>());
        return Results.Ok(await GetTraceStepsAsync(userId, appJobId.Value));
    }

    public async Task<IResult> GetLeadAuditAsync(Guid userId, Guid jobLeadId)
    {
        var appJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        if (appJobId is null)
        {
            return Results.Ok(new
            {
                fields = Array.Empty<object>(),
                answers = Array.Empty<object>(),
                steps = Array.Empty<object>(),
                traceSteps = Array.Empty<object>(),
                checkpoints = Array.Empty<object>(),
                events = Array.Empty<object>(),
                learnings = Array.Empty<object>()
            });
        }

        return await GetApplicationJobAuditAsync(userId, appJobId.Value);
    }

    public async Task<IResult> GetApplicationJobAuditAsync(Guid userId, Guid applicationJobId)
    {
        var job = await _db.ApplicationJobs
            .Include(x => x.JobLead)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == applicationJobId);
        if (job is null) return Results.NotFound();

        return Results.Ok(new
        {
            job,
            jobLead = job.JobLead,
            fields = await GetFieldsAsync(userId, applicationJobId),
            answers = await GetAnswersAsync(userId, applicationJobId),
            steps = await GetStepsAsync(userId, applicationJobId),
            traceSteps = await GetTraceStepsAsync(userId, applicationJobId),
            checkpoints = await GetCheckpointsAsync(userId, applicationJobId),
            events = await GetEventsAsync(userId, applicationJobId),
            learnings = await GetLearningsAsync(userId, applicationJobId)
        });
    }

    public async Task<List<Domain.ApplicationField>> GetFieldsAsync(Guid userId, Guid applicationJobId) =>
        await _db.ApplicationFields
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

    public async Task<List<Domain.ApplicationAnswer>> GetAnswersAsync(Guid userId, Guid applicationJobId) =>
        await _db.ApplicationAnswers
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

    public async Task<List<Domain.AgentStep>> GetStepsAsync(Guid userId, Guid applicationJobId) =>
        await _db.AgentSteps
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderBy(x => x.StepNumber)
            .ToListAsync();

    public async Task<List<Domain.AgentTraceStep>> GetTraceStepsAsync(Guid userId, Guid applicationJobId) =>
        await _db.AgentTraceSteps
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderBy(x => x.StepNumber)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

    public async Task<List<Domain.Checkpoint>> GetCheckpointsAsync(Guid userId, Guid applicationJobId) =>
        await _db.Checkpoints
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<List<Domain.ExecutionEvent>> GetEventsAsync(Guid userId, Guid applicationJobId) =>
        await _db.ExecutionEvents
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync();

    public async Task<List<Domain.AgentLearning>> GetLearningsAsync(Guid userId, Guid applicationJobId) =>
        await _db.AgentLearnings
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
}
