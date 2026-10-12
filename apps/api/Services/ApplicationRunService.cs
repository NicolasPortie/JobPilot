using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationRunService(
    IRepository<CandidateProfile> profiles,
    IRepository<JobLead> jobLeads,
    IRepository<ApplicationRun> runs,
    IUnitOfWork unitOfWork,
    JobFilterService filterService,
    ScoringService scoring,
    ApplicationResumePreparationService resumePreparation)
{
    public async Task<ServiceResult<ApplicationRunCreationResult>> CreateAsync(Guid userId, CreateRunRequest request)
    {
        var profile = await profiles.Query().FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await filterService.GetOrCreateAsync(userId);
        var jobs = await jobLeads.Query()
            .Where(x => x.UserId == userId && request.JobLeadIds.Contains(x.Id))
            .ToListAsync();
        var queueable = new List<JobLead>();

        foreach (var lead in jobs)
        {
            scoring.Score(lead, profile, filter);
            if (filterService.ShouldIgnore(lead, filter, out var reason))
            {
                lead.Status = JobStatuses.Ignored;
                lead.FilteredReason = reason;
                continue;
            }

            queueable.Add(lead);
            lead.FilteredReason = "";
        }

        var run = new ApplicationRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            TotalJobs = queueable.Count
        };
        var queuedTaskIds = new List<Guid>();
        foreach (var lead in queueable)
        {
            lead.Status = JobStatuses.QueuedToApply;
            var taskId = Guid.NewGuid();
            queuedTaskIds.Add(taskId);
            run.Jobs.Add(new ApplicationJob
            {
                Id = taskId,
                UserId = userId,
                JobLeadId = lead.Id,
                JobUrl = lead.Url,
                State = JobStatuses.QueuedToApply
            });
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();

        runs.Add(run);
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.CommitAsync();

        await resumePreparation.PrepareQueuedAsync(userId, queuedTaskIds);
        return ServiceResult<ApplicationRunCreationResult>.Created(
            new ApplicationRunCreationResult(ApplicationRunResponse.From(run), queueable.Count, jobs.Count - queueable.Count));
    }

    public async Task<List<ApplicationRunResponse>> ListAsync(Guid userId) =>
        (await runs.Query()
            .Include(x => x.Jobs)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync())
        .Select(ApplicationRunResponse.From)
        .ToList();
}

public sealed record ApplicationRunCreationResult(ApplicationRunResponse Run, int Queued, int Ignored);
