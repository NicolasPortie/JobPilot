using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationRunService
{
    private readonly JobPilotDbContext _db;
    private readonly JobFilterService _filterService;
    private readonly ScoringService _scoring;
    private readonly ApplicationResumePreparationService _resumePreparation;

    public ApplicationRunService(JobPilotDbContext db, JobFilterService filterService, ScoringService scoring, ApplicationResumePreparationService resumePreparation)
    {
        _db = db;
        _filterService = filterService;
        _scoring = scoring;
        _resumePreparation = resumePreparation;
    }

    public async Task<IResult> CreateAsync(Guid userId, CreateRunRequest request)
    {
        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await _filterService.GetOrCreateAsync(userId);
        var jobs = await _db.JobLeads.Where(x => x.UserId == userId && request.JobLeadIds.Contains(x.Id)).ToListAsync();
        var queueable = new List<JobLead>();

        foreach (var lead in jobs)
        {
            _scoring.Score(lead, profile, filter);
            if (_filterService.ShouldIgnore(lead, filter, out var reason))
            {
                lead.Status = JobStatuses.Ignored;
                lead.FilteredReason = reason;
                continue;
            }

            queueable.Add(lead);
            lead.FilteredReason = "";
        }

        var run = new ApplicationRun { Id = Guid.NewGuid(), UserId = userId, Name = request.Name, TotalJobs = queueable.Count };
        var queuedTaskIds = new List<Guid>();
        foreach (var lead in queueable)
        {
            lead.Status = JobStatuses.QueuedToApply;
            var taskId = Guid.NewGuid();
            queuedTaskIds.Add(taskId);
            run.Jobs.Add(new ApplicationJob { Id = taskId, UserId = userId, JobLeadId = lead.Id, JobUrl = lead.Url, State = JobStatuses.QueuedToApply });
        }

        _db.ApplicationRuns.Add(run);
        await _db.SaveChangesAsync();
        await _resumePreparation.PrepareQueuedAsync(userId, queuedTaskIds);
        return Results.Created($"/api/runs/{run.Id}", new { run, queued = queueable.Count, ignored = jobs.Count - queueable.Count });
    }

    public async Task<List<ApplicationRun>> ListAsync(Guid userId) =>
        await _db.ApplicationRuns
            .Include(x => x.Jobs)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
}
