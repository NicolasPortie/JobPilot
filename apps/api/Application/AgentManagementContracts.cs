namespace JobPilot.Api.Application;

public sealed record AgentPolicyRequest(
    string AutoSubmitMode,
    string ExecutionTarget,
    int MaxApplicationsPerRun,
    int MaxApplicationsPerDay,
    string[] AllowedDomains,
    string[] BlockedDomains,
    bool IsPaused);

public sealed record AgentPolicyResponse(
    Guid Id,
    string AutoSubmitMode,
    string ExecutionTarget,
    int MaxApplicationsPerRun,
    int MaxApplicationsPerDay,
    string[] AllowedDomains,
    string[] BlockedDomains,
    bool IsPaused,
    DateTimeOffset UpdatedAt);

public sealed record AgentHistoryResponse(
    IReadOnlyList<AgentRunHistoryItem> Runs,
    IReadOnlyList<AgentStepHistoryItem> Steps,
    IReadOnlyList<ApplicationFieldHistoryItem> Fields,
    IReadOnlyList<ApplicationAnswerHistoryItem> Answers,
    IReadOnlyList<SiteWorkflowMemoryHistoryItem> Memories);

public sealed record AgentRunHistoryItem(
    Guid Id,
    Guid? ApplicationJobId,
    string AgentName,
    string Mode,
    string State,
    string PolicyMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentStepHistoryItem(
    Guid Id,
    Guid? AgentRunId,
    Guid? ApplicationJobId,
    int StepNumber,
    string AgentName,
    string PageState,
    string ActionType,
    string TargetLabel,
    string Result,
    string EvidencePath,
    decimal Confidence,
    DateTimeOffset CreatedAt);

public sealed record ApplicationFieldHistoryItem(
    Guid Id,
    Guid ApplicationJobId,
    string FieldKey,
    string Label,
    string FieldType,
    string ValueSource,
    decimal Confidence,
    DateTimeOffset CreatedAt);

public sealed record ApplicationAnswerHistoryItem(
    Guid Id,
    Guid ApplicationJobId,
    string QuestionText,
    string AnswerValue,
    string Source,
    string ProviderModel,
    decimal Confidence,
    DateTimeOffset CreatedAt);

public sealed record SiteWorkflowMemoryHistoryItem(
    Guid Id,
    string Domain,
    string PatternKey,
    string ActionJson,
    int SuccessCount,
    DateTimeOffset UpdatedAt);
