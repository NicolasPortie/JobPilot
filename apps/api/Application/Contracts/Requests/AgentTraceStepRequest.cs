namespace JobPilot.Api.Application.Contracts;

public record AgentTraceStepRequest(Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string ObjectiveKind, string ObjectiveJson, string Platform, string Module, string Phase, string BeforeFingerprint, string AfterFingerprint, string Planner, string ProposalJson, string RiskJson, string GuardVerdictJson, string ExecutionJson, string OutcomeJson, string RecoveryJson, string ContentSafetyJson, string EvidencePath, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);
