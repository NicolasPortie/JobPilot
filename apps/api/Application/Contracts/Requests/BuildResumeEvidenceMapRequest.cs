namespace JobPilot.Api.Application.Contracts;

public record BuildResumeEvidenceMapRequest(Guid ResumeVersionId, Guid JobTargetId);
