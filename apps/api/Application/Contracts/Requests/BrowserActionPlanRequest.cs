namespace JobPilot.Api.Application.Contracts;

public record BrowserActionPlanRequest(
    string Objective,
    string PageState,
    string ObservationJson,
    string? JobContext,
    Guid? ApplicationJobId = null,
    int Attempt = 1,
    string? PreviousFailure = null,
    string? DecisionMemory = null);
