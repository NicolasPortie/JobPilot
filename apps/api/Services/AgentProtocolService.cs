using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AgentProtocolService(
    IRepository<CandidateProfile> profiles,
    IRepository<ReusableAnswer> answers,
    IRepository<AgentRun> runs,
    IRepository<AgentStep> steps,
    IRepository<AgentTraceStep> traces,
    IRepository<ApplicationField> fields,
    IRepository<ApplicationAnswer> applicationAnswers,
    IRepository<SiteWorkflowMemory> memories,
    IRepository<ExecutionEvent> events,
    IRepository<Checkpoint> checkpoints,
    IUnitOfWork unitOfWork,
    IAiChatService ai,
    TraceRedactionService redactor) : IAgentProtocolService
{
    public async Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, AnswerDraftRequest request)
    {
        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        return await ai.DraftAnswerAsync(userId, request.QuestionText, request.JobContext, profile);
    }

    public async Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request)
    {
        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var reusableAnswers = await answers.ListAsync(x => x.UserId == userId);
        return await ai.PlanBrowserActionAsync(userId, request, profile, reusableAnswers);
    }

    public async Task<AgentRun> StartRunAsync(Guid userId, AgentRunRequest request)
    {
        var run = new AgentRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            AgentName = request.AgentName,
            Mode = request.Mode,
            PolicyMode = request.PolicyMode,
            State = "running"
        };
        runs.Add(run);
        await unitOfWork.SaveChangesAsync();
        return run;
    }

    public async Task<ServiceResult<AgentRun>> CompleteRunAsync(Guid userId, Guid id, AgentRunCompleteRequest request)
    {
        var run = await runs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (run is null)
            return ServiceResult<AgentRun>.NotFound();

        run.State = request.State;
        run.CompletedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AgentRun>.Ok(run);
    }

    public async Task SaveStepAsync(Guid userId, AgentStepRequest request)
    {
        steps.Add(new AgentStep
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<Guid> SaveTraceStepAsync(Guid userId, AgentTraceStepRequest request)
    {
        var trace = new AgentTraceStep
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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
        traces.Add(trace);
        await unitOfWork.SaveChangesAsync();
        return trace.Id;
    }

    public async Task SaveApplicationFieldAsync(Guid userId, ApplicationFieldRequest request)
    {
        fields.Add(new ApplicationField
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            FieldKey = request.FieldKey,
            Label = request.Label,
            FieldType = request.FieldType,
            ValueSource = request.ValueSource,
            Confidence = request.Confidence
        });
        await unitOfWork.SaveChangesAsync();
    }

    public async Task SaveApplicationAnswerAsync(Guid userId, ApplicationAnswerRequest request)
    {
        applicationAnswers.Add(new ApplicationAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            QuestionText = request.QuestionText,
            AnswerValue = request.AnswerValue,
            Source = request.Source,
            ProviderModel = request.ProviderModel,
            Confidence = request.Confidence
        });
        await unitOfWork.SaveChangesAsync();
    }

    public async Task SaveWorkflowMemoryAsync(Guid userId, SiteWorkflowMemoryRequest request)
    {
        if (!request.Success)
            return;

        var memory = await memories.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Domain == request.Domain && x.PatternKey == request.PatternKey);
        if (memory is null)
        {
            memories.Add(new SiteWorkflowMemory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Domain = request.Domain,
                PatternKey = request.PatternKey,
                ActionJson = request.ActionJson,
                SuccessCount = 1
            });
        }
        else
        {
            memory.ActionJson = request.ActionJson;
            memory.SuccessCount++;
            memory.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await unitOfWork.SaveChangesAsync();
    }

    public async Task SaveEventAsync(Guid userId, ExecutionEventRequest request)
    {
        events.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = request.ApplicationJobId,
            EventType = request.EventType,
            Detail = request.Detail,
            OccurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow
        });
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<Checkpoint> CreateCheckpointAsync(Guid userId, AgentCheckpointRequest request)
    {
        var checkpoint = new Checkpoint
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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
        checkpoints.Add(checkpoint);
        await unitOfWork.SaveChangesAsync();
        return checkpoint;
    }
}
