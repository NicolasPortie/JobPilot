namespace JobPilot.Api.Application.Contracts;

public record TailoredResumeResponse(
    Guid TailoredVersionId,
    Guid BaseResumeVersionId,
    Guid JobTargetId,
    Guid EvidenceMapRunId,
    string Status,
    int AtsScore,
    ResumeScoreComponent[] ScoreComponents,
    ResumeSectionDiff[] SectionDiffs,
    ResumeReviewQuestion[] ReviewQuestions,
    string PlainText,
    string[] IncludedKeywords,
    string[] AdjacentKeywords,
    string[] ExcludedKeywords,
    string[] Checkpoints,
    string[] Warnings,
    ResumeVersionDetail Version,
    bool UsedAi = false,
    string? AiNotice = null);
