using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record ResumeReviewAnswerResponse(
    Guid Id,
    Guid? EvidenceMapRunId,
    Guid JobRequirementId,
    string RequirementLabel,
    string QuestionText,
    string AnswerValue,
    string AnswerType,
    string EvidenceText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ResumeReviewAnswerResponse From(ResumeReviewAnswer answer) =>
        new(
            answer.Id,
            answer.ResumeEvidenceMapRunId,
            answer.JobRequirementId,
            answer.RequirementLabel,
            answer.QuestionText,
            answer.AnswerValue,
            answer.AnswerType,
            answer.EvidenceText,
            answer.CreatedAt,
            answer.UpdatedAt);
}
