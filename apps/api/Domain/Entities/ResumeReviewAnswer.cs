namespace JobPilot.Api.Domain;

public class ResumeReviewAnswer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ResumeEvidenceMapRunId { get; set; }
    public Guid JobRequirementId { get; set; }
    public string RequirementLabel { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public string AnswerValue { get; set; } = "";
    public string AnswerType { get; set; } = "unknown_confirmed"; // unknown_confirmed, proven, adjacent, missing
    public string EvidenceText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
