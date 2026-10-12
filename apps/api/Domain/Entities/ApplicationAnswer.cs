namespace JobPilot.Api.Domain;

public class ApplicationAnswer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationJobId { get; set; }
    public string QuestionText { get; set; } = "";
    public string AnswerValue { get; set; } = "";
    public string Source { get; set; } = "";
    public string ProviderModel { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
