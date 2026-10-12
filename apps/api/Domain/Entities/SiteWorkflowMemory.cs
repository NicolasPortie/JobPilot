namespace JobPilot.Api.Domain;

public class SiteWorkflowMemory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Domain { get; set; } = "";
    public string PatternKey { get; set; } = "";
    public string ActionJson { get; set; } = "";
    public int SuccessCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
