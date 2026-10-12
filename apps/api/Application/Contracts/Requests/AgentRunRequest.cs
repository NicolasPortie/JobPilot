namespace JobPilot.Api.Application.Contracts;

public record AgentRunRequest(Guid? ApplicationJobId, string AgentName, string Mode, string PolicyMode);
