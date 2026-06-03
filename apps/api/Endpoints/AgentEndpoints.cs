using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder api)
    {
        var agent = api.MapGroup("/agent").RequireAuthorization();

        agent.MapPost("/answer-draft", async (AnswerDraftRequest request, IAiChatService ai, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.AgentUserId();
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            return Results.Ok(await ai.DraftAnswerAsync(userId, request.QuestionText, request.JobContext, profile));
        });

        agent.MapGet("/job-filters", async (JobFilterService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.GetOrCreateAsync(principal.AgentUserId())));

        agent.MapGet("/profile/validate", async (ProfileGuard guard, ClaimsPrincipal principal) =>
        {
            var issues = await guard.ValidateAsync(principal.AgentUserId());
            return Results.Ok(new { valid = issues.All(x => !x.IsCritical), critical = issues.Any(x => x.IsCritical), issues });
        });

        agent.MapGet("/tasks/{id:guid}", async (Guid id, AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.GetTaskAsync(principal.AgentUserId(), id));

        agent.MapGet("/tasks/next", async (AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.GetNextTaskAsync(principal.AgentUserId()));

        agent.MapPost("/jobs/discovered", async (DiscoveredJobsRequest request, AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.SaveDiscoveredJobsAsync(principal.AgentUserId(), request));

        agent.MapPost("/auto-queue", async (AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.AutoQueueAsync(principal.AgentUserId()));

        agent.MapPost("/page-state", async (PageStateRequest request, IAiChatService ai, ClaimsPrincipal principal) =>
            Results.Ok(await ai.ClassifyPageStateAsync(principal.AgentUserId(), request)));

        agent.MapPost("/step-validation", async (StepValidationRequest request, IAiChatService ai, ClaimsPrincipal principal) =>
            Results.Ok(await ai.ValidateApplicationStepAsync(principal.AgentUserId(), request)));

        agent.MapPost("/browser-action-plan", async (BrowserActionPlanRequest request, IAiChatService ai, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var userId = principal.AgentUserId();
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            var answers = await db.ReusableAnswers.Where(x => x.UserId == userId).ToListAsync();
            return Results.Ok(await ai.PlanBrowserActionAsync(userId, request, profile, answers));
        });

        agent.MapPost("/resolve-answer", async (ResolveAgentAnswerRequest request, AgentAnswerResolutionService answers, ClaimsPrincipal principal) =>
            Results.Ok(await answers.ResolveAsync(principal.AgentUserId(), request)));

        agent.MapPost("/runs", async (AgentRunRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var run = new AgentRun
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                ApplicationJobId = request.ApplicationJobId,
                AgentName = request.AgentName,
                Mode = request.Mode,
                PolicyMode = request.PolicyMode,
                State = "running"
            };
            db.AgentRuns.Add(run);
            await db.SaveChangesAsync();
            return Results.Created($"/api/agent/runs/{run.Id}", run);
        });

        agent.MapPatch("/runs/{id:guid}", async (Guid id, AgentRunCompleteRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            var run = await db.AgentRuns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == principal.AgentUserId());
            if (run is null) return Results.NotFound();
            run.State = request.State;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(run);
        });

        agent.MapPost("/steps", async (AgentStepRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            db.AgentSteps.Add(new AgentStep
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                AgentRunId = request.AgentRunId,
                ApplicationJobId = request.ApplicationJobId,
                StepNumber = request.StepNumber,
                AgentName = request.AgentName,
                PageState = request.PageState,
                ActionType = request.ActionType,
                TargetLabel = request.TargetLabel,
                Result = request.Result,
                EvidencePath = request.EvidencePath,
                Confidence = request.Confidence
            });
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        agent.MapPost("/trace-steps", async (AgentTraceStepRequest request, JobPilotDbContext db, TraceRedactionService redactor, ClaimsPrincipal principal) =>
        {
            var trace = new AgentTraceStep
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                AgentRunId = request.AgentRunId,
                ApplicationJobId = request.ApplicationJobId,
                StepNumber = request.StepNumber,
                ObjectiveKind = request.ObjectiveKind,
                ObjectiveJson = redactor.Redact(request.ObjectiveJson),
                Platform = request.Platform,
                Module = request.Module,
                Phase = request.Phase,
                BeforeFingerprint = request.BeforeFingerprint,
                AfterFingerprint = request.AfterFingerprint,
                Planner = request.Planner,
                ProposalJson = redactor.Redact(request.ProposalJson),
                RiskJson = redactor.Redact(request.RiskJson),
                GuardVerdictJson = redactor.Redact(request.GuardVerdictJson),
                ExecutionJson = redactor.Redact(request.ExecutionJson),
                OutcomeJson = redactor.Redact(request.OutcomeJson),
                RecoveryJson = redactor.Redact(request.RecoveryJson),
                ContentSafetyJson = redactor.Redact(request.ContentSafetyJson),
                EvidencePath = redactor.Redact(request.EvidencePath),
                StartedAt = request.StartedAt,
                CompletedAt = request.CompletedAt,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.AgentTraceSteps.Add(trace);
            await db.SaveChangesAsync();
            return Results.Created($"/api/agent/trace-steps/{trace.Id}", new AgentTraceStepResponse(trace.Id));
        });

        agent.MapPost("/application-fields", async (ApplicationFieldRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            db.ApplicationFields.Add(new ApplicationField
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                ApplicationJobId = request.ApplicationJobId,
                FieldKey = request.FieldKey,
                Label = request.Label,
                FieldType = request.FieldType,
                ValueSource = request.ValueSource,
                Confidence = request.Confidence
            });
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        agent.MapPost("/application-answers", async (ApplicationAnswerRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            db.ApplicationAnswers.Add(new ApplicationAnswer
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                ApplicationJobId = request.ApplicationJobId,
                QuestionText = request.QuestionText,
                AnswerValue = request.AnswerValue,
                Source = request.Source,
                ProviderModel = request.ProviderModel,
                Confidence = request.Confidence
            });
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        agent.MapPost("/site-workflow-memory", async (SiteWorkflowMemoryRequest request, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            if (!request.Success) return Results.Accepted();

            var userId = principal.AgentUserId();
            var memory = await db.SiteWorkflowMemories.FirstOrDefaultAsync(x => x.UserId == userId && x.Domain == request.Domain && x.PatternKey == request.PatternKey);
            if (memory is null)
            {
                memory = new SiteWorkflowMemory { Id = Guid.NewGuid(), UserId = userId, Domain = request.Domain, PatternKey = request.PatternKey, ActionJson = request.ActionJson, SuccessCount = 1 };
                db.SiteWorkflowMemories.Add(memory);
            }
            else
            {
                memory.ActionJson = request.ActionJson;
                memory.SuccessCount++;
                memory.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        agent.MapGet("/learnings", async (string? status, string? type, string? risk, string? domain, AgentLearningService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.ListAsync(principal.AgentUserId(), status, type, risk, domain)));

        agent.MapGet("/learnings/active", async (AgentLearningService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.ActiveAsync(principal.AgentUserId())));

        agent.MapGet("/learnings/{id:guid}", async (Guid id, AgentLearningService service, ClaimsPrincipal principal) =>
            await service.GetAsync(principal.AgentUserId(), id) is { } learning ? Results.Ok(learning) : Results.NotFound());

        agent.MapPost("/learnings", async (AgentLearningRequest request, AgentLearningService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.UpsertCandidateAsync(principal.AgentUserId(), request)));

        agent.MapPost("/learnings/{id:guid}/approve", async (Guid id, AgentLearningReviewRequest request, AgentLearningService service, ClaimsPrincipal principal) =>
            await service.ReviewAsync(principal.AgentUserId(), principal.AgentUserId(), id, "approve") is { } learning ? Results.Ok(learning) : Results.NotFound());

        agent.MapPost("/learnings/{id:guid}/reject", async (Guid id, AgentLearningReviewRequest request, AgentLearningService service, ClaimsPrincipal principal) =>
            await service.ReviewAsync(principal.AgentUserId(), principal.AgentUserId(), id, "reject") is { } learning ? Results.Ok(learning) : Results.NotFound());

        agent.MapPost("/learnings/{id:guid}/disable", async (Guid id, AgentLearningReviewRequest request, AgentLearningService service, ClaimsPrincipal principal) =>
            await service.ReviewAsync(principal.AgentUserId(), principal.AgentUserId(), id, "disable") is { } learning ? Results.Ok(learning) : Results.NotFound());

        agent.MapPost("/learnings/generate-from-job/{applicationJobId:guid}", async (Guid applicationJobId, AgentLearningService service, ClaimsPrincipal principal) =>
            Results.Ok(await service.GenerateFromJobAsync(principal.AgentUserId(), applicationJobId)));

        agent.MapPost("/learnings/outcome", async (AgentLearningOutcomeRequest request, AgentLearningService service, ClaimsPrincipal principal) =>
            await service.RecordOutcomeAsync(principal.AgentUserId(), request) is { } learning ? Results.Ok(learning) : Results.NotFound());

        agent.MapPatch("/tasks/{id:guid}/state", async (Guid id, AgentStateRequest request, AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.UpdateStateAsync(principal.AgentUserId(), id, request));

        agent.MapPatch("/tasks/{id:guid}/apply-surface", async (Guid id, ApplySurfaceRequest request, AgentTaskQueueService tasks, ClaimsPrincipal principal) =>
            await tasks.UpdateApplySurfaceAsync(principal.AgentUserId(), id, request));

        agent.MapPost("/events", async (ExecutionEvent input, JobPilotDbContext db, ClaimsPrincipal principal) =>
        {
            input.Id = Guid.NewGuid();
            input.UserId = principal.AgentUserId();
            db.ExecutionEvents.Add(input);
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        agent.MapPost("/checkpoints", async (AgentCheckpointRequest request, JobPilotDbContext db, TraceRedactionService redactor, ClaimsPrincipal principal) =>
        {
            var input = new Checkpoint
            {
                Id = Guid.NewGuid(),
                UserId = principal.AgentUserId(),
                ApplicationJobId = request.ApplicationJobId,
                AgentRunId = request.AgentRunId,
                TraceStepId = request.TraceStepId,
                ReasonCode = request.ReasonCode,
                Stage = request.Stage,
                FieldLabel = redactor.Redact(request.FieldLabel),
                Detail = redactor.Redact(request.Detail),
                SuggestedUserAction = redactor.Redact(request.SuggestedUserAction),
                ResumeMode = string.IsNullOrWhiteSpace(request.ResumeMode) ? "reobserve" : request.ResumeMode,
                ResolutionStatus = "open"
            };
            db.Checkpoints.Add(input);
            await db.SaveChangesAsync();
            return Results.Created($"/api/checkpoints/{input.Id}", input);
        });

        return agent;
    }
}
