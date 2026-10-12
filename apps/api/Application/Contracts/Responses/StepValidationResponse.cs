namespace JobPilot.Api.Application.Contracts;

public record StepValidationResponse(bool Success, string Reason, decimal Confidence, bool RequiresCheckpoint);
