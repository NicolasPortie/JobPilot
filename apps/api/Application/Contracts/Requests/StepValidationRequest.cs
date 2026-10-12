namespace JobPilot.Api.Application.Contracts;

public record StepValidationRequest(string ActionJson, string BeforeObservationJson, string AfterObservationJson, string? JobContext);
