namespace JobPilot.Api.Application.Contracts;

public record JobFilterRequest(string[] AllowedSeniorities, string[] BlockedSeniorities, string[] AllowedRoleKeywords, string[] BlockedRoleKeywords, string[] AllowedWorkplaceTypes, string[] BlockedWorkplaceTypes, string[] AllowedApplyTypes, int MinScore, bool AutoIgnoreLowScore);
