namespace JobPilot.Api.Domain;

public class JobLead
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public string Platform { get; set; } = "linkedin";
    public string ApplyPlatform { get; set; } = "unknown";
    public string ApplySurface { get; set; } = "unknown";
    public string ApplyUrl { get; set; } = "";
    public string AutomationSupport { get; set; } = "unknown";
    public string AutomationReason { get; set; } = "";
    public int Score { get; set; }
    public string ScoreReason { get; set; } = "";
    public string Status { get; set; } = JobStatuses.Found;
    public bool IsRemote { get; set; }
    public string FilteredReason { get; set; } = "";
    public DateTimeOffset FoundAt { get; set; } = DateTimeOffset.UtcNow;
}
