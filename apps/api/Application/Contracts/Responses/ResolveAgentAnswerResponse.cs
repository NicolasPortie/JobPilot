namespace JobPilot.Api.Application.Contracts;

public record ResolveAgentAnswerResponse(string AnswerValue, string Source, decimal Confidence, string Reason, bool RequiresCheckpoint);
