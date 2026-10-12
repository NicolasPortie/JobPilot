namespace JobPilot.Api.Application.Contracts;

public record AgentStepRequest(Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string AgentName, string PageState, string ActionType, string TargetLabel, string Result, string EvidencePath, decimal Confidence);
