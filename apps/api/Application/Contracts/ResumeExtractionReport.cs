namespace JobPilot.Api.Application.Contracts;

public record ResumeExtractionReport(
    string SourceKind,
    string ExtractionMethod,
    string Classification,
    int TextQualityScore,
    decimal Confidence,
    bool RequiresOcr,
    bool RequiresReview,
    int Pages,
    int PagesWithNativeText,
    int CharacterCount,
    int WordCount,
    string[] Warnings);
