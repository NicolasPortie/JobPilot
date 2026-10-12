namespace JobPilot.Api.Application.Contracts;

public record ResumePreviewResponse(Guid ResumeVersionId, string Layout, string Html);
