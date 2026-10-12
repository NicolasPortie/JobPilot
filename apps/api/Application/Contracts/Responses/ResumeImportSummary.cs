namespace JobPilot.Api.Application.Contracts;

public record ResumeImportSummary(Guid Id, string SourceFileName, string SourceKind, string ExtractionMethod, string Classification, int TextQualityScore, decimal Confidence, bool RequiresOcr, bool RequiresReview, int WordCount, DateTimeOffset CreatedAt);
