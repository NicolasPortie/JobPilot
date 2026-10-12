namespace JobPilot.Api.Application.Contracts;

public record ResumeRequirementEvidenceDto(Guid Id, Guid JobRequirementId, string RequirementLabel, string RequirementCategory, string RequirementImportance, string MatchStatus, string EvidenceText, string EvidenceSource, string Reason, decimal Confidence, int SortOrder);
