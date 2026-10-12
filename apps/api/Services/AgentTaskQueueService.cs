using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AgentTaskQueueService
{
    private readonly JobPilotDbContext _db;
    private readonly JobFilterService _filterService;
    private readonly ScoringService _scoring;
    private readonly IAiChatService _ai;
    private readonly IConfiguration _configuration;
    private readonly ApplicationLimitService _limits;
    private readonly ApplicationResumePreparationService _resumePreparation;

    public AgentTaskQueueService(JobPilotDbContext db, JobFilterService filterService, ScoringService scoring, IAiChatService ai, IConfiguration configuration, ApplicationLimitService limits, ApplicationResumePreparationService resumePreparation)
    {
        _db = db;
        _filterService = filterService;
        _scoring = scoring;
        _ai = ai;
        _configuration = configuration;
        _limits = limits;
        _resumePreparation = resumePreparation;
    }

    public async Task<ServiceResult<AgentTaskResponse>> GetTaskAsync(Guid userId, Guid taskId)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.UserId == userId && x.Id == taskId);
        if (task is null) return ServiceResult<AgentTaskResponse>.NotFound();

        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var answers = await _db.ReusableAnswers.Where(x => x.UserId == userId).ToListAsync();
        return ServiceResult<AgentTaskResponse>.Ok(AgentTaskResponse.From(task, task.JobLead!, profile, answers));
    }

    public async Task<ServiceResult<AgentTaskResponse>> GetNextTaskAsync(Guid userId)
    {
        if (await GetPausedPolicyAsync(userId) is not null)
        {
            await RecordPolicyPausedAsync(userId, "agent.next_task");
            return ServiceResult<AgentTaskResponse>.NoContent();
        }

        var limit = await _limits.GetDailySnapshotAsync(userId);
        if (limit.IsReached)
        {
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = "agent.daily_limit_reached",
                Detail = $"Daily application limit reached: appliedToday={limit.AppliedToday}; limit={limit.DailyLimit}."
            });
            await _db.SaveChangesAsync();
            return ServiceResult<AgentTaskResponse>.NoContent();
        }

        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await _filterService.GetOrCreateAsync(userId);

        while (true)
        {
            var task = await _db.ApplicationJobs
                .Include(x => x.JobLead)
                .Where(x => x.UserId == userId && x.State == JobStatuses.QueuedToApply)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (task is null) return ServiceResult<AgentTaskResponse>.NoContent();

            if (task.JobLead is not null)
            {
                var leadStatus = task.JobLead.Status;
                _scoring.Score(task.JobLead, profile, filter);
                if (_filterService.ShouldIgnore(task.JobLead, filter, out var reason))
                {
                    task.State = JobStatuses.Ignored;
                    task.Reason = reason;
                    task.JobLead.Status = JobStatuses.Ignored;
                    task.JobLead.FilteredReason = reason;
                    await _db.SaveChangesAsync();
                    continue;
                }

                if (!IsSupportedApplyPlatformForQueue(task.JobLead, filter, out var supportReason))
                {
                    task.State = JobStatuses.ManualOnly;
                    task.Reason = supportReason;
                    task.JobLead.Status = JobStatuses.ManualOnly;
                    task.JobLead.AutomationSupport = "manual_only";
                    task.JobLead.AutomationReason = supportReason;
                    await _db.SaveChangesAsync();
                    continue;
                }

                task.JobLead.Status = leadStatus;
            }

            var answers = await _db.ReusableAnswers.Where(x => x.UserId == userId).ToListAsync();
            task.State = JobStatuses.Opening;
            if (task.JobLead is not null) task.JobLead.Status = JobStatuses.Opening;
            await _db.SaveChangesAsync();
            return ServiceResult<AgentTaskResponse>.Ok(AgentTaskResponse.From(task, task.JobLead!, profile, answers));
        }
    }

    public async Task<ServiceResult<DiscoveredJobsResult>> SaveDiscoveredJobsAsync(Guid userId, DiscoveredJobsRequest request)
    {
        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await _filterService.GetOrCreateAsync(userId);
        var saved = 0;
        var ignored = 0;
        var reviewed = 0;
        var searchHarnessEnabled = _configuration.GetValue("SearchHarness:Enabled", true);
        var maxLlmReviews = Math.Max(0, _configuration.GetValue("SearchHarness:MaxReviewsPerBatch", 12));

        foreach (var item in request.Jobs)
        {
            if (DiscoveredJobMapper.IsInvalid(item))
            {
                ignored++;
                continue;
            }

            var existing = await _db.JobLeads.FirstOrDefaultAsync(x => x.UserId == userId && x.Url == item.Url);
            var lead = existing ?? item.ToLead(userId);
            var protectedStatus = existing is not null && DiscoveredJobMapper.IsProtectedStatus(existing.Status) ? existing.Status : null;

            if (existing is not null)
            {
                if (protectedStatus is not null)
                {
                    existing.FoundAt = DateTimeOffset.UtcNow;
                    saved++;
                    continue;
                }

                lead.Title = item.Title;
                lead.Company = item.Company;
                lead.Location = item.Location;
                lead.Description = item.Description;
                lead.IsRemote = item.IsRemote;
                lead.FoundAt = DateTimeOffset.UtcNow;
            }

            _scoring.Score(lead, profile, filter);
            if (_filterService.ShouldIgnore(lead, filter, out var reason))
            {
                if (protectedStatus is null)
                {
                    lead.Status = JobStatuses.Ignored;
                    lead.FilteredReason = reason;
                }
                else
                {
                    lead.Status = protectedStatus;
                }

                if (existing is null) _db.JobLeads.Add(lead);
                if (protectedStatus is null) ignored++;
                else saved++;
                continue;
            }

            if (searchHarnessEnabled && protectedStatus is null && reviewed < maxLlmReviews)
            {
                reviewed++;
                var review = await _ai.ReviewJobFitAsync(userId, lead, profile, filter);
                DiscoveredJobMapper.ApplyReview(lead, review);

                if (lead.Status == JobStatuses.Ignored)
                {
                    if (existing is null) _db.JobLeads.Add(lead);
                    ignored++;
                    continue;
                }
            }
            else if (searchHarnessEnabled && reviewed >= maxLlmReviews)
            {
                lead.FilteredReason = "search_harness_pending: batch review limit reached";
                lead.Status = JobStatuses.NeedsReview;
            }

            if (protectedStatus is not null)
            {
                lead.Status = protectedStatus;
                lead.FilteredReason = "";
            }

            if (lead.Status != JobStatuses.Applied && lead.Status != JobStatuses.QueuedToApply && lead.Status != JobStatuses.Opening && lead.Status != JobStatuses.Filling && lead.Status != JobStatuses.ManualOnly && lead.Status != JobStatuses.NeedsReview)
            {
                lead.Status = JobStatuses.Scored;
            }
            if (lead.Status == JobStatuses.Scored) lead.FilteredReason = "";
            if (existing is null) _db.JobLeads.Add(lead);
            saved++;
        }

        await _db.SaveChangesAsync();
        return ServiceResult<DiscoveredJobsResult>.Ok(new DiscoveredJobsResult(saved, ignored, reviewed));
    }

    public async Task<ServiceResult<AutoQueueResult>> AutoQueueAsync(Guid userId)
    {
        if (await GetPausedPolicyAsync(userId) is not null)
        {
            await RecordPolicyPausedAsync(userId, "agent.auto_queue");
            return ServiceResult<AutoQueueResult>.Ok(new AutoQueueResult(0, 0, Paused: true));
        }

        var limit = await _limits.GetDailySnapshotAsync(userId);
        if (limit.IsReached)
            return ServiceResult<AutoQueueResult>.Ok(
                new AutoQueueResult(
                    0,
                    0,
                    DailyLimitReached: true,
                    DailyLimit: limit.DailyLimit,
                    AppliedToday: limit.AppliedToday,
                    RemainingToday: limit.RemainingToday));

        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var filter = await _filterService.GetOrCreateAsync(userId);
        var leads = await _db.JobLeads
            .Where(x => x.UserId == userId && (x.Status == JobStatuses.Scored || x.Status == JobStatuses.Found || x.Status == JobStatuses.Shortlisted))
            .ToListAsync();

        var run = new ApplicationRun { Id = Guid.NewGuid(), UserId = userId, Name = $"Auto-queue {DateTimeOffset.Now:yyyy-MM-dd HH:mm}", TotalJobs = 0 };
        var queued = 0;
        var queuedTaskIds = new List<Guid>();

        foreach (var lead in leads)
        {
            var hasExistingApplication = await _db.ApplicationJobs.AnyAsync(x => x.UserId == userId && x.JobLeadId == lead.Id);
            if (hasExistingApplication) continue;
            if (lead.Status == JobStatuses.Applied) continue;

            var originalStatus = lead.Status;
            if (lead.Status == JobStatuses.Found || lead.Score <= 0)
                _scoring.Score(lead, profile, filter);

            if (_filterService.ShouldIgnore(lead, filter, out var filterReason))
            {
                lead.Status = JobStatuses.Ignored;
                lead.FilteredReason = filterReason;
                continue;
            }

            if (!IsSupportedApplyPlatformForQueue(lead, filter, out var supportReason))
            {
                lead.Status = JobStatuses.ManualOnly;
                lead.AutomationSupport = "manual_only";
                lead.AutomationReason = supportReason;
                continue;
            }

            lead.Status = JobStatuses.QueuedToApply;
            lead.FilteredReason = "";
            lead.AutomationReason = originalStatus == JobStatuses.Shortlisted ? "auto_queue_shortlisted" : lead.AutomationReason;
            var taskId = Guid.NewGuid();
            queuedTaskIds.Add(taskId);
            run.Jobs.Add(new ApplicationJob { Id = taskId, UserId = userId, ApplicationRunId = run.Id, JobLeadId = lead.Id, JobUrl = lead.Url, State = JobStatuses.QueuedToApply });
            queued++;
            if (queued >= limit.RemainingToday)
                break;
        }

        run.TotalJobs = queued;
        if (queued > 0) _db.ApplicationRuns.Add(run);
        await _db.SaveChangesAsync();
        if (queuedTaskIds.Count > 0)
            await _resumePreparation.PrepareQueuedAsync(userId, queuedTaskIds);
        return ServiceResult<AutoQueueResult>.Ok(new AutoQueueResult(queued, leads.Count));
    }

    public async Task<ServiceResult<ApplicationJobResponse>> UpdateStateAsync(Guid userId, Guid taskId, AgentStateRequest request)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.Id == taskId && x.UserId == userId);
        if (task is null) return ServiceResult<ApplicationJobResponse>.NotFound();

        var previousState = task.State;
        task.State = request.State;
        task.Reason = request.Reason;
        if (task.JobLead is not null) task.JobLead.Status = request.State;

        if (!previousState.Equals(JobStatuses.Applied, StringComparison.OrdinalIgnoreCase) &&
            request.State.Equals(JobStatuses.Applied, StringComparison.OrdinalIgnoreCase) &&
            !await _db.ExecutionEvents.AnyAsync(x => x.UserId == userId && x.ApplicationJobId == task.Id && x.EventType == "job.applied"))
        {
            _db.ExecutionEvents.Add(new ExecutionEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationJobId = task.Id,
                EventType = "job.applied",
                Detail = string.IsNullOrWhiteSpace(request.Reason) ? "State changed to applied." : request.Reason
            });
        }

        await _db.SaveChangesAsync();
        return ServiceResult<ApplicationJobResponse>.Ok(ApplicationJobResponse.From(task));
    }

    public async Task<ServiceResult<JobLeadResponse>> UpdateApplySurfaceAsync(Guid userId, Guid taskId, ApplySurfaceRequest request)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.Id == taskId && x.UserId == userId);
        if (task?.JobLead is null) return ServiceResult<JobLeadResponse>.NotFound();

        task.JobLead.ApplyPlatform = Normalize(request.ApplyPlatform, "unknown");
        task.JobLead.ApplySurface = Normalize(request.ApplySurface, "unknown");
        task.JobLead.ApplyUrl = request.ApplyUrl ?? "";
        task.JobLead.AutomationSupport = Normalize(request.AutomationSupport, "unknown");
        task.JobLead.AutomationReason = request.AutomationReason ?? "";
        await _db.SaveChangesAsync();
        return ServiceResult<JobLeadResponse>.Ok(JobLeadResponse.From(task.JobLead));
    }

    private static bool IsSupportedApplyPlatformForQueue(JobLead lead, JobFilter filter, out string reason)
    {
        reason = "";
        var applyPlatform = Normalize(lead.ApplyPlatform, "unknown");
        if (applyPlatform == "unknown")
            return true;

        var allowed = filter.AllowedApplyTypes ?? [];
        if (allowed.Contains(applyPlatform, StringComparer.OrdinalIgnoreCase) ||
            applyPlatform == "linkedin_easy_apply" && allowed.Contains("easy_apply", StringComparer.OrdinalIgnoreCase) ||
            applyPlatform == "inhire" && allowed.Contains("external_form", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        reason = $"unsupported_apply_platform:{applyPlatform}";
        return false;
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private async Task<AgentPolicy?> GetPausedPolicyAsync(Guid userId) =>
        await _db.AgentPolicies.FirstOrDefaultAsync(x => x.UserId == userId && x.IsPaused);

    private async Task RecordPolicyPausedAsync(Guid userId, string action)
    {
        _db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "agent.policy.paused",
            Detail = $"Agent policy is paused; action={action}."
        });
        await _db.SaveChangesAsync();
    }
}
