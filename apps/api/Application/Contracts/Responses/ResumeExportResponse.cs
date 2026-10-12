namespace JobPilot.Api.Application.Contracts;

public record ResumeExportResponse(Guid Id, Guid ResumeVersionId, string Format, string[] ParseWarnings, DateTimeOffset CreatedAt);
