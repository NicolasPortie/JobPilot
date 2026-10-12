namespace JobPilot.Api.Application.Contracts;

public record ApplicationJobResumeRequest(Guid ResumeVersionId, Guid? ResumeExportId = null);
