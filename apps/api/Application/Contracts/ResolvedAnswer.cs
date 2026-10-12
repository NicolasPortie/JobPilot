namespace JobPilot.Api.Application.Contracts;

public record ResolvedAnswer(string AnswerValue, string Source, decimal Confidence, string Reason);
