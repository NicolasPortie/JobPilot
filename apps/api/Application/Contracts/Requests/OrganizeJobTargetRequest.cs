namespace JobPilot.Api.Application.Contracts;

public record OrganizeJobTargetRequest(string SourceKind, string? Text, string? Url, bool UseAi = true);
