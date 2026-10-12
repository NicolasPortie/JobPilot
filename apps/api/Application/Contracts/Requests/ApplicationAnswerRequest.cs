namespace JobPilot.Api.Application.Contracts;

public record ApplicationAnswerRequest(Guid ApplicationJobId, string QuestionText, string AnswerValue, string Source, string ProviderModel, decimal Confidence);
