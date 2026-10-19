namespace JobPilot.Api.Application.Contracts;

public sealed record AiProviderTestResponse(bool Ok, string Provider, string Model, string Sample, string? Error);
