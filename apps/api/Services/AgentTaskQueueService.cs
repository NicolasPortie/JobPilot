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

    public async Task<IResult> GetTaskAsync(Guid userId, Guid taskId)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.UserId == userId && x.Id == taskId);
        if (task is null) return Results.NotFound();

        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var answers = await _db.ReusableAnswers.Where(x => x.UserId == userId).ToListAsync();
        return Results.Ok(new AgentTaskResponse(task, task.JobLead!, profile, answers));
    }

    public async Task<IResult> GetNextTaskAsync(Guid userId)
    {
        if (await GetPausedPolicyAsync(userId) is not null)
        {
            await RecordPolicyPausedAsync(userId, "agent.next_task");
            return Results.NoContent();
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
            return Results.NoContent();
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

            if (task is null) return Results.NoContent();

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
            return Results.Ok(new AgentTaskResponse(task, task.JobLead!, profile, answers));
        }
    }

    public async Task<IResult> SaveDiscoveredJobsAsync(Guid userId, DiscoveredJobsRequest request)
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
            if (IsInvalidDiscoveredJob(item))
            {
                ignored++;
                continue;
            }

            var existing = await _db.JobLeads.FirstOrDefaultAsync(x => x.UserId == userId && x.Url == item.Url);
            var lead = existing ?? item.ToLead(userId);
            var protectedStatus = existing is not null && IsProtectedStatus(existing.Status) ? existing.Status : null;

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
                ApplySearchReview(lead, review);

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
        return Results.Ok(new { saved, ignored, reviewed });
    }

    private static bool IsProtectedStatus(string status) =>
        status is JobStatuses.Applied
            or JobStatuses.QueuedToApply
            or JobStatuses.Opening
            or JobStatuses.Filling
            or JobStatuses.ReadyForSubmit
            or JobStatuses.ManualOnly
            or JobStatuses.NeedsReview;

    private static bool IsInvalidDiscoveredJob(DiscoveredJobDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Url))
            return true;

        var normalizedTitle = RemoveDiacritics(item.Title).ToLowerInvariant();
        return normalizedTitle.Contains("volte a experimentar o premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("experimente o premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("try premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("premium gratis", StringComparison.Ordinal) ||
               normalizedTitle.Contains("premium free", StringComparison.Ordinal);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static void ApplySearchReview(JobLead lead, JobFitReviewResponse review)
    {
        var reason = $"search_harness: {review.Reason}";
        var details = BuildSearchReviewDetails(review);

        lead.Score = review.FitScore;
        lead.ScoreReason = string.IsNullOrWhiteSpace(details)
            ? reason
            : $"{reason}; {details}";

        switch (review.Decision)
        {
            case "ignore":
                lead.Status = JobStatuses.Ignored;
                lead.FilteredReason = reason;
                break;
            case "review":
                lead.Status = JobStatuses.NeedsReview;
                lead.FilteredReason = reason;
                break;
            default:
                lead.Status = JobStatuses.Scored;
                lead.FilteredReason = "";
                break;
        }
    }

    private static string BuildSearchReviewDetails(JobFitReviewResponse review)
    {
        var parts = new List<string>
        {
            $"fit={review.FitScore}",
            $"confidence={review.Confidence:0.00}",
            $"seniority={review.DetectedSeniority}",
            $"workplace={review.DetectedWorkplace}",
            $"apply={review.ApplyType}"
        };

        if (review.Strengths.Length > 0) parts.Add("strengths=" + string.Join(", ", review.Strengths));
        if (review.Risks.Length > 0) parts.Add("risks=" + string.Join(", ", review.Risks));
        return string.Join("; ", parts);
    }

    public async Task<IResult> AutoQueueAsync(Guid userId)
    {
        if (await GetPausedPolicyAsync(userId) is not null)
        {
            await RecordPolicyPausedAsync(userId, "agent.auto_queue");
            return Results.Ok(new { queued = 0, total = 0, paused = true });
        }

        var limit = await _limits.GetDailySnapshotAsync(userId);
        if (limit.IsReached)
            return Results.Ok(new { queued = 0, total = 0, dailyLimitReached = true, limit.DailyLimit, limit.AppliedToday, limit.RemainingToday });

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
        return Results.Ok(new { queued, total = leads.Count });
    }

    public async Task<IResult> UpdateStateAsync(Guid userId, Guid taskId, AgentStateRequest request)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.Id == taskId && x.UserId == userId);
        if (task is null) return Results.NotFound();

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
        return Results.Ok(task);
    }

    public async Task<IResult> UpdateApplySurfaceAsync(Guid userId, Guid taskId, ApplySurfaceRequest request)
    {
        var task = await _db.ApplicationJobs.Include(x => x.JobLead).FirstOrDefaultAsync(x => x.Id == taskId && x.UserId == userId);
        if (task?.JobLead is null) return Results.NotFound();

        task.JobLead.ApplyPlatform = Normalize(request.ApplyPlatform, "unknown");
        task.JobLead.ApplySurface = Normalize(request.ApplySurface, "unknown");
        task.JobLead.ApplyUrl = request.ApplyUrl ?? "";
        task.JobLead.AutomationSupport = Normalize(request.AutomationSupport, "unknown");
        task.JobLead.AutomationReason = request.AutomationReason ?? "";
        await _db.SaveChangesAsync();
        return Results.Ok(task.JobLead);
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
