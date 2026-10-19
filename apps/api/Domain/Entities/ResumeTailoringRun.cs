namespace JobPilot.Api.Domain;

public class ResumeTailoringRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public string ProviderModel { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
