namespace JobPilot.Api.Application.Contracts;

public record BuildGenericAtsResumeRequest(Guid BaseResumeVersionId, string Layout = "ats_standard", bool UseAi = false);
