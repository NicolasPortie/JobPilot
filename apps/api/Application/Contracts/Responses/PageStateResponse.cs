namespace JobPilot.Api.Application.Contracts;

public record PageStateResponse(string State, string Reason, decimal Confidence);
