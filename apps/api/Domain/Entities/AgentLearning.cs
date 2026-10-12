namespace JobPilot.Api.Domain;

public class AgentLearning
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string Domain { get; set; } = "";
    public string LearningType { get; set; } = AgentLearningTypes.Technical;
    public string PatternKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string TriggerJson { get; set; } = "{}";
    public string ActionJson { get; set; } = "{}";
    public string EvidenceJson { get; set; } = "{}";
    public string Status { get; set; } = AgentLearningStatuses.Candidate;
    public string RiskLevel { get; set; } = AgentLearningRiskLevels.Low;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    public bool IsActive => Status is AgentLearningStatuses.Approved or AgentLearningStatuses.AutoApproved;
}
