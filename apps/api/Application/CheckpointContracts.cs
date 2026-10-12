using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record CheckpointResponse(
    Guid Id,
    Guid? ApplicationJobId,
    Guid? AgentRunId,
    Guid? TraceStepId,
    string ReasonCode,
    string Stage,
    string FieldLabel,
    string Detail,
    string ResolutionStatus,
    string? ResolutionValue,
    string SuggestedUserAction,
    string ResumeMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt)
{
    public static CheckpointResponse From(Checkpoint checkpoint) =>
        new(
            checkpoint.Id,
            checkpoint.ApplicationJobId,
            checkpoint.AgentRunId,
            checkpoint.TraceStepId,
            checkpoint.ReasonCode,
            checkpoint.Stage,
            checkpoint.FieldLabel,
            checkpoint.Detail,
            checkpoint.ResolutionStatus,
            checkpoint.ResolutionValue,
            checkpoint.SuggestedUserAction,
            checkpoint.ResumeMode,
            checkpoint.CreatedAt,
            checkpoint.ResolvedAt);
}
