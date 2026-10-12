namespace JobPilot.Api.Domain;

public class AgentStep
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public int StepNumber { get; set; }
    public string AgentName { get; set; } = "";
    public string PageState { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string TargetLabel { get; set; } = "";
    public string Result { get; set; } = "";
    public string EvidencePath { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
