namespace JobPilot.Api.Domain;

public class AgentTraceStep
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public int StepNumber { get; set; }
    public string ObjectiveKind { get; set; } = "";
    public string ObjectiveJson { get; set; } = "{}";
    public string Platform { get; set; } = "";
    public string Module { get; set; } = "";
    public string Phase { get; set; } = "";
    public string BeforeFingerprint { get; set; } = "";
    public string AfterFingerprint { get; set; } = "";
    public string Planner { get; set; } = "";
    public string ProposalJson { get; set; } = "{}";
    public string RiskJson { get; set; } = "{}";
    public string GuardVerdictJson { get; set; } = "{}";
    public string ExecutionJson { get; set; } = "{}";
    public string OutcomeJson { get; set; } = "{}";
    public string RecoveryJson { get; set; } = "{}";
    public string ContentSafetyJson { get; set; } = "{}";
    public string EvidencePath { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
