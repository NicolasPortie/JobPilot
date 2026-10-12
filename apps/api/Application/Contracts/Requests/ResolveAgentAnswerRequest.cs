namespace JobPilot.Api.Application.Contracts;

public record ResolveAgentAnswerRequest(Guid ApplicationJobId, string QuestionText, string FieldContext, string JobContext, string FieldType);
