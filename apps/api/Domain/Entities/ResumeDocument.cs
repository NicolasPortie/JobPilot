namespace JobPilot.Api.Domain;

public class ResumeDocument
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ResumeImportId { get; set; }
    public string Title { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string Status { get; set; } = "draft";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ResumeVersion> Versions { get; set; } = [];
}
