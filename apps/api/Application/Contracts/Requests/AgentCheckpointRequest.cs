namespace JobPilot.Api.Application.Contracts;

public record AgentCheckpointRequest(Guid? ApplicationJobId, Guid? AgentRunId, Guid? TraceStepId, string ReasonCode, string Stage, string FieldLabel, string Detail, string SuggestedUserAction, string ResumeMode);
