namespace JobPilot.Api.Domain;

public class ApplicationRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string State { get; set; } = "queued";
    public int TotalJobs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ApplicationJob> Jobs { get; set; } = [];
}
