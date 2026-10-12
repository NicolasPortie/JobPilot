namespace JobPilot.Api.Application.Contracts;

public record BrowserActionPlanResponse(string ActionType, string ElementId, string Value, string Reason, decimal Confidence, bool RequiresCheckpoint);
