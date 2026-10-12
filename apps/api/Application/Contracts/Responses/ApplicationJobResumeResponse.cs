namespace JobPilot.Api.Application.Contracts;

public record ApplicationJobResumeResponse(Guid ApplicationJobId, Guid ResumeVersionId, Guid? ResumeExportId, string ResumeFilePath);
