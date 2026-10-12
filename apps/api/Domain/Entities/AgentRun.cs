namespace JobPilot.Api.Domain;

public class AgentRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string AgentName { get; set; } = "";
    public string Mode { get; set; } = "";
    public string State { get; set; } = "running";
    public string PolicyMode { get; set; } = "conservative";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
