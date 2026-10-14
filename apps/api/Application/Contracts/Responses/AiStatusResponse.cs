namespace JobPilot.Api.Application.Contracts;

public sealed record AiStatusResponse(bool Available, bool Configured, string Provider, string Model, bool? LastCallSucceeded, DateTimeOffset? LastCheckedAt, string Message);
