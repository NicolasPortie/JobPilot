namespace JobPilot.Api.Application.Contracts;

public record JobRequirementDto(Guid Id, string Category, string Label, string Importance, string EvidenceText, string NormalizedKeyword, string[] ExactPhrases, int SortOrder);
