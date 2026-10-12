namespace JobPilot.Api.Application.Contracts;

public record ProfileValidationIssue(string FieldName, string ErrorMessage, bool IsCritical);
