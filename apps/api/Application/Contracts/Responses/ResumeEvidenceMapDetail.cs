namespace JobPilot.Api.Application.Contracts;

public record ResumeEvidenceMapDetail(Guid Id, Guid ResumeVersionId, Guid JobTargetId, string Status, int EvidenceScore, decimal Confidence, string[] Warnings, ResumeRequirementEvidenceDto[] Items, DateTimeOffset CreatedAt);
