namespace JobPilot.Api.Application.Contracts;

public record TailorResumeRequest(Guid BaseResumeVersionId, Guid JobTargetId, Guid? EvidenceMapRunId = null, string Layout = "ats_standard", bool UseAi = true);
