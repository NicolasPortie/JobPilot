using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public static class ValueSources
{
    public const string CandidateProfile = "candidate-profile";
    public const string ReusableAnswer = "reusable-answer";
    public const string QuestionPattern = "question-pattern";
    public const string AiDraft = "ai-draft";
    public const string FallbackPolicy = "fallback-policy";
    public const string ManualRequired = "manual-required";
}

public static class ReasonCodes
{
    public const string MissingRequiredField = "missing_required_field";
    public const string InvalidExistingValue = "invalid_existing_value";
    public const string AnswerNeedsConfirmation = "answer_needs_confirmation";
    public const string SensitiveAnswerMissingSource = "sensitive_answer_missing_source";
    public const string ValidationFailed = "validation_failed";
    public const string StuckDetection = "stuck_detection";
    public const string LoginRequired = "login_required";
    public const string CaptchaOrSecurity = "captcha_or_security";
}

public sealed record FieldIssue(
    ElementCandidate Field,
    string IssueType,
    SemanticFieldType InferredType,
    string Reason);

public sealed record ResolvedFieldValue(
    string Value,
    string Source,
    decimal Confidence,
    SemanticFieldType InferredType,
    bool RequiresReview,
    string Reason);

public sealed record FormAuditResult(
    string PageState,
    IReadOnlyList<FieldIssue> Issues,
    IReadOnlyList<ElementCandidate> Fields,
    IReadOnlyList<ElementCandidate> Buttons);

public sealed record ActionPlan(ActionProposal Proposal, string Risk)
{
    public BrowserAction Action => Proposal.ToBrowserAction();

    public ActionPlan(BrowserAction action, string risk)
        : this(ActionProposal.FromAction(action, "local", risk), risk)
    {
    }
}
