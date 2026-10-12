namespace JobPilot.Api.Domain;

public class ResumeExport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public string Format { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ParseText { get; set; } = "";
    public string ParseWarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
