namespace JobPilot.Api.Domain;

public class ResumeImport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SourceFileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string ExtractionMethod { get; set; } = "";
    public string Classification { get; set; } = "";
    public int TextQualityScore { get; set; }
    public decimal Confidence { get; set; }
    public bool RequiresOcr { get; set; }
    public bool RequiresReview { get; set; }
    public int Pages { get; set; }
    public int PagesWithNativeText { get; set; }
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public string RawText { get; set; } = "";
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
