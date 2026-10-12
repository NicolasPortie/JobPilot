namespace JobPilot.Api.Application.Contracts;

public sealed record ExecutionEventRequest(Guid? ApplicationJobId, string EventType, string Detail, DateTimeOffset? OccurredAt);
