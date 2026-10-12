namespace JobPilot.Api.Application.Contracts;

public record ResumeReviewQuestion(Guid RequirementId, string RequirementLabel, string Category, string Importance, string QuestionText, string Reason);
