namespace JobPilot.Api.Domain;

public class AiUsageLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AiProviderId { get; set; }
    public string ProviderType { get; set; } = "";
    public string ApiFormat { get; set; } = "";
    public string Model { get; set; } = "";
    public string Operation { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public string UsageJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
