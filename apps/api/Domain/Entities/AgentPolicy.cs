namespace JobPilot.Api.Domain;

public class AgentPolicy
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AutoSubmitMode { get; set; } = "conservative";
    public string ExecutionTarget { get; set; } = ExecutionTargets.Extension;
    public int MaxApplicationsPerRun { get; set; } = 10;
    public int MaxApplicationsPerDay { get; set; } = 50;
    public string[] AllowedDomains { get; set; } = [];
    public string[] BlockedDomains { get; set; } = [];
    public bool IsPaused { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(AgentPolicy input)
    {
        AutoSubmitMode = input.AutoSubmitMode;
        ExecutionTarget = ExecutionTargets.Normalize(input.ExecutionTarget);
        MaxApplicationsPerRun = input.MaxApplicationsPerRun;
        MaxApplicationsPerDay = input.MaxApplicationsPerDay;
        AllowedDomains = input.AllowedDomains;
        BlockedDomains = input.BlockedDomains;
        IsPaused = input.IsPaused;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
