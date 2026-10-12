using System.Text.Json.Serialization;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record ApplicationJobAuditSummary(
    Guid Id,
    Guid JobLeadId,
    Guid? ResumeVersionId,
    Guid? ResumeExportId,
    string ResumeFilePath,
    string JobUrl,
    string State,
    string? Reason,
    DateTimeOffset CreatedAt)
{
    public static ApplicationJobAuditSummary From(ApplicationJob job) =>
        new(job.Id, job.JobLeadId, job.ResumeVersionId, job.ResumeExportId, job.ResumeFilePath, job.JobUrl, job.State, job.Reason, job.CreatedAt);
}

public sealed record JobLeadAuditSummary(
    Guid Id,
    string Title,
    string Company,
    string Location,
    string Url,
    string Platform,
    string Status,
    int Score)
{
    public static JobLeadAuditSummary From(JobLead lead) =>
        new(lead.Id, lead.Title, lead.Company, lead.Location, lead.Url, lead.Platform, lead.Status, lead.Score);
}

public sealed record ApplicationFieldAuditResponse(Guid Id, Guid ApplicationJobId, string FieldKey, string Label, string FieldType, string ValueSource, decimal Confidence, DateTimeOffset CreatedAt)
{
    public static ApplicationFieldAuditResponse From(ApplicationField item) =>
        new(item.Id, item.ApplicationJobId, item.FieldKey, item.Label, item.FieldType, item.ValueSource, item.Confidence, item.CreatedAt);
}

public sealed record ApplicationAnswerAuditResponse(Guid Id, Guid ApplicationJobId, string QuestionText, string AnswerValue, string Source, string ProviderModel, decimal Confidence, DateTimeOffset CreatedAt)
{
    public static ApplicationAnswerAuditResponse From(ApplicationAnswer item) =>
        new(item.Id, item.ApplicationJobId, item.QuestionText, item.AnswerValue, item.Source, item.ProviderModel, item.Confidence, item.CreatedAt);
}

public sealed record AgentStepAuditResponse(Guid Id, Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string AgentName, string PageState, string ActionType, string TargetLabel, string Result, string EvidencePath, decimal Confidence, DateTimeOffset CreatedAt)
{
    public static AgentStepAuditResponse From(AgentStep item) =>
        new(item.Id, item.AgentRunId, item.ApplicationJobId, item.StepNumber, item.AgentName, item.PageState, item.ActionType, item.TargetLabel, item.Result, item.EvidencePath, item.Confidence, item.CreatedAt);
}

public sealed record AgentTraceStepAuditResponse(Guid Id, Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string ObjectiveKind, string ObjectiveJson, string Platform, string Module, string Phase, string BeforeFingerprint, string AfterFingerprint, string Planner, string ProposalJson, string RiskJson, string GuardVerdictJson, string ExecutionJson, string OutcomeJson, string RecoveryJson, string ContentSafetyJson, string EvidencePath, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, DateTimeOffset CreatedAt)
{
    public static AgentTraceStepAuditResponse From(AgentTraceStep item) =>
        new(item.Id, item.AgentRunId, item.ApplicationJobId, item.StepNumber, item.ObjectiveKind, item.ObjectiveJson, item.Platform, item.Module, item.Phase, item.BeforeFingerprint, item.AfterFingerprint, item.Planner, item.ProposalJson, item.RiskJson, item.GuardVerdictJson, item.ExecutionJson, item.OutcomeJson, item.RecoveryJson, item.ContentSafetyJson, item.EvidencePath, item.StartedAt, item.CompletedAt, item.CreatedAt);
}

public sealed record CheckpointAuditResponse(Guid Id, Guid? ApplicationJobId, Guid? AgentRunId, Guid? TraceStepId, string ReasonCode, string Stage, string FieldLabel, string Detail, string ResolutionStatus, string? ResolutionValue, string SuggestedUserAction, string ResumeMode, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt)
{
    public static CheckpointAuditResponse From(Checkpoint item) =>
        new(item.Id, item.ApplicationJobId, item.AgentRunId, item.TraceStepId, item.ReasonCode, item.Stage, item.FieldLabel, item.Detail, item.ResolutionStatus, item.ResolutionValue, item.SuggestedUserAction, item.ResumeMode, item.CreatedAt, item.ResolvedAt);
}

public sealed record ExecutionEventAuditResponse(Guid Id, Guid? ApplicationJobId, string EventType, string Detail, DateTimeOffset OccurredAt)
{
    public static ExecutionEventAuditResponse From(ExecutionEvent item) =>
        new(item.Id, item.ApplicationJobId, item.EventType, item.Detail, item.OccurredAt);
}

public sealed record AgentLearningAuditResponse(Guid Id, Guid? ApplicationJobId, string Domain, string LearningType, string PatternKey, string Title, string Summary, string TriggerJson, string ActionJson, string EvidenceJson, string Status, string RiskLevel, int SuccessCount, int FailureCount, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt)
{
    public static AgentLearningAuditResponse From(AgentLearning item) =>
        new(item.Id, item.ApplicationJobId, item.Domain, item.LearningType, item.PatternKey, item.Title, item.Summary, item.TriggerJson, item.ActionJson, item.EvidenceJson, item.Status, item.RiskLevel, item.SuccessCount, item.FailureCount, item.CreatedAt, item.ReviewedAt);
}

public sealed record ApplicationAuditResult(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ApplicationJobAuditSummary? Job,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JobLeadAuditSummary? JobLead,
    IReadOnlyList<ApplicationFieldAuditResponse> Fields,
    IReadOnlyList<ApplicationAnswerAuditResponse> Answers,
    IReadOnlyList<AgentStepAuditResponse> Steps,
    IReadOnlyList<AgentTraceStepAuditResponse> TraceSteps,
    IReadOnlyList<CheckpointAuditResponse> Checkpoints,
    IReadOnlyList<ExecutionEventAuditResponse> Events,
    IReadOnlyList<AgentLearningAuditResponse> Learnings);
