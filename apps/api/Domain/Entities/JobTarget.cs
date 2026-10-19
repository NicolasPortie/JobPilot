namespace JobPilot.Api.Domain;

public class JobTarget
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SourceKind { get; set; } = "manual_text";
    public string SourceText { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Seniority { get; set; } = "unknown";
    public string Workplace { get; set; } = "unknown";
    public string Summary { get; set; } = "";
    public string Status { get; set; } = "organized";
    public decimal Confidence { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<JobRequirement> Requirements { get; set; } = [];
}
