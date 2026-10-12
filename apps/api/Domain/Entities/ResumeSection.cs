namespace JobPilot.Api.Domain;

public class ResumeSection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public string SectionType { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public string ContentJson { get; set; } = "{}";
    public ResumeVersion? Version { get; set; }
}
