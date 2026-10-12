namespace JobPilot.Api.Application.Contracts;

public record JobRequirementItem(string Category, string Label, string Importance, string EvidenceText, string NormalizedKeyword, string[] ExactPhrases);
