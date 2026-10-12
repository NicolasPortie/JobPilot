namespace JobPilot.Api.Domain;

public class ExecutionEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string EventType { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
