using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationAuditService(
    IRepository<ApplicationJob> jobs,
    IRepository<ApplicationField> fields,
    IRepository<ApplicationAnswer> answers,
    IRepository<AgentStep> steps,
    IRepository<AgentTraceStep> traces,
    IRepository<Checkpoint> checkpoints,
    IRepository<ExecutionEvent> events,
    IRepository<AgentLearning> learnings)
{
    public async Task<Guid?> FindApplicationJobIdByLeadAsync(Guid userId, Guid jobLeadId)
    {
        var applicationJob = await jobs.FirstOrDefaultAsync(item => item.JobLeadId == jobLeadId && item.UserId == userId);
        return applicationJob?.Id;
    }

    public async Task<ServiceResult<List<ApplicationFieldAuditResponse>>> GetFieldsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<ApplicationFieldAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetFieldsAsync));

    public async Task<ServiceResult<List<ApplicationAnswerAuditResponse>>> GetAnswersByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<ApplicationAnswerAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetAnswersAsync));

    public async Task<ServiceResult<List<AgentStepAuditResponse>>> GetStepsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<AgentStepAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetStepsAsync));

    public async Task<ServiceResult<List<AgentTraceStepAuditResponse>>> GetTraceStepsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<AgentTraceStepAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetTraceStepsAsync));

    public async Task<ServiceResult<List<CheckpointAuditResponse>>> GetCheckpointsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<CheckpointAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetCheckpointsAsync));

    public async Task<ServiceResult<List<ExecutionEventAuditResponse>>> GetEventsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<ExecutionEventAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetEventsAsync));

    public async Task<ServiceResult<List<AgentLearningAuditResponse>>> GetLearningsByLeadAsync(Guid userId, Guid jobLeadId) =>
        ServiceResult<List<AgentLearningAuditResponse>>.Ok(await ForLeadAsync(userId, jobLeadId, GetLearningsAsync));

    public async Task<ServiceResult<ApplicationAuditResult>> GetLeadAuditAsync(Guid userId, Guid jobLeadId)
    {
        var applicationJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        return applicationJobId is null
            ? ServiceResult<ApplicationAuditResult>.Ok(Empty())
            : await GetApplicationJobAuditAsync(userId, applicationJobId.Value);
    }

    public async Task<ServiceResult<ApplicationAuditResult>> GetApplicationJobAuditAsync(Guid userId, Guid applicationJobId)
    {
        var job = await jobs.Query()
            .Include(item => item.JobLead)
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Id == applicationJobId);
        if (job is null) return ServiceResult<ApplicationAuditResult>.NotFound();

        return ServiceResult<ApplicationAuditResult>.Ok(new ApplicationAuditResult(
            ApplicationJobAuditSummary.From(job),
            job.JobLead is null ? null : JobLeadAuditSummary.From(job.JobLead),
            await GetFieldsAsync(userId, applicationJobId),
            await GetAnswersAsync(userId, applicationJobId),
            await GetStepsAsync(userId, applicationJobId),
            await GetTraceStepsAsync(userId, applicationJobId),
            await GetCheckpointsAsync(userId, applicationJobId),
            await GetEventsAsync(userId, applicationJobId),
            await GetLearningsAsync(userId, applicationJobId)));
    }

    public async Task<List<ApplicationFieldAuditResponse>> GetFieldsAsync(Guid userId, Guid applicationJobId) =>
        (await fields.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderBy(item => item.CreatedAt).ToListAsync())
        .Select(ApplicationFieldAuditResponse.From).ToList();

    public async Task<List<ApplicationAnswerAuditResponse>> GetAnswersAsync(Guid userId, Guid applicationJobId) =>
        (await answers.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderBy(item => item.CreatedAt).ToListAsync())
        .Select(ApplicationAnswerAuditResponse.From).ToList();

    public async Task<List<AgentStepAuditResponse>> GetStepsAsync(Guid userId, Guid applicationJobId) =>
        (await steps.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderBy(item => item.StepNumber).ToListAsync())
        .Select(AgentStepAuditResponse.From).ToList();

    public async Task<List<AgentTraceStepAuditResponse>> GetTraceStepsAsync(Guid userId, Guid applicationJobId) =>
        (await traces.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderBy(item => item.StepNumber).ThenBy(item => item.CreatedAt).ToListAsync())
        .Select(AgentTraceStepAuditResponse.From).ToList();

    public async Task<List<CheckpointAuditResponse>> GetCheckpointsAsync(Guid userId, Guid applicationJobId) =>
        (await checkpoints.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderByDescending(item => item.CreatedAt).ToListAsync())
        .Select(CheckpointAuditResponse.From).ToList();

    public async Task<List<ExecutionEventAuditResponse>> GetEventsAsync(Guid userId, Guid applicationJobId) =>
        (await events.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderBy(item => item.OccurredAt).ToListAsync())
        .Select(ExecutionEventAuditResponse.From).ToList();

    public async Task<List<AgentLearningAuditResponse>> GetLearningsAsync(Guid userId, Guid applicationJobId) =>
        (await learnings.Query().Where(item => item.UserId == userId && item.ApplicationJobId == applicationJobId).OrderByDescending(item => item.CreatedAt).ToListAsync())
        .Select(AgentLearningAuditResponse.From).ToList();

    private async Task<List<T>> ForLeadAsync<T>(Guid userId, Guid jobLeadId, Func<Guid, Guid, Task<List<T>>> load)
    {
        var applicationJobId = await FindApplicationJobIdByLeadAsync(userId, jobLeadId);
        return applicationJobId is null ? [] : await load(userId, applicationJobId.Value);
    }

    private static ApplicationAuditResult Empty() => new(null, null, [], [], [], [], [], [], []);
}
