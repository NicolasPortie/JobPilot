namespace JobPilot.Api.Application.Contracts;

public record ResumeDocumentSummary(Guid Id, string Title, string Status, string SourceFileName, string SourceKind, DateTimeOffset UpdatedAt, ResumeVersionSummary[] Versions);
