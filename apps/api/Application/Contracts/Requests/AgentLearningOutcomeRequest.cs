namespace JobPilot.Api.Application.Contracts;

public record AgentLearningOutcomeRequest(string PatternKey, bool Success, Guid? ApplicationJobId, string? Detail);
