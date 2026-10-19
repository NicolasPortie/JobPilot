namespace JobPilot.Api.Domain;

public class ResumeVersion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeDocumentId { get; set; }
    public string Title { get; set; } = "";
    public string TargetCompany { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public string Status { get; set; } = "draft";
    public int AtsScore { get; set; }
    public string AtsReportJson { get; set; } = "{}";
    public string PlainText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ResumeDocument? Document { get; set; }
    public List<ResumeSection> Sections { get; set; } = [];
}
