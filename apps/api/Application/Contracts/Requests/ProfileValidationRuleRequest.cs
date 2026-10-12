namespace JobPilot.Api.Application.Contracts;

public record ProfileValidationRuleRequest(string FieldName, string ValidationType, string ValidationValue, string ErrorMessage, bool IsCritical);
