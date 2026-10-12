namespace JobPilot.Api.Domain;

public class Checkpoint
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? TraceStepId { get; set; }
    public string ReasonCode { get; set; } = "";
    public string Stage { get; set; } = "";
    public string FieldLabel { get; set; } = "";
    public string Detail { get; set; } = "";
    public string ResolutionStatus { get; set; } = "open";
    public string? ResolutionValue { get; set; }
    public string SuggestedUserAction { get; set; } = "";
    public string ResumeMode { get; set; } = "reobserve";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
