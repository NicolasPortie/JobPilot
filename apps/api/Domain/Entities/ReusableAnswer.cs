namespace JobPilot.Api.Domain;

public class ReusableAnswer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string QuestionKey { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public string AnswerValue { get; set; } = "";
    public string Category { get; set; } = "general";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
