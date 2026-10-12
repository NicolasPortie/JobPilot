namespace JobPilot.Api.Application.Contracts;

public record QuestionPatternRequest(string PatternKey, string Category, string[] Tags, string[] QuestionTemplates, string AnswerTemplate, string ValueSource, string ProfileFieldMapping, decimal ConfidenceThreshold);
