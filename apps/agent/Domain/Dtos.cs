using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JobPilot.Agent.Domain;

public enum SemanticFieldType
{
    Unknown,
    FullName, FirstName, LastName,
    Email, Phone, Cpf, Cnpj,
    Address, City, Country, ZipCode,
    LinkedInUrl, GitHubUrl, PortfolioUrl,
    Salary, BirthDate, StartDate, NoticePeriod,
    ExperienceYears,
    ResumeUpload, CoverLetterUpload,
    Gender, Race, Disability, VeteranStatus,
    YesNoChoice, MultiChoice, FreeText
}

public sealed record DiscoveredJobDto(string Title, string Company, string Location, string Description, string Url, bool IsRemote);

public sealed class CandidateProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Cpf { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "Brazil";
    public string LinkedinUrl { get; set; } = "";
    public string GithubUrl { get; set; } = "";
    public string PortfolioUrl { get; set; } = "";
    public string ProfessionalSummary { get; set; } = "";
    public string[] Skills { get; set; } = [];
    public string TargetRole { get; set; } = "";
    public string Seniority { get; set; } = "junior";
    public string SalaryExpectation { get; set; } = "";
    public string ResumePath { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReusableAnswer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string QuestionKey { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public string AnswerValue { get; set; } = "";
    public string Category { get; set; } = "general";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class QuestionPattern
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PatternKey { get; set; } = "";
    public string Category { get; set; } = "general";
    public string[] Tags { get; set; } = [];
    public string[] QuestionTemplates { get; set; } = [];
    public string AnswerTemplate { get; set; } = "";
    public string ValueSource { get; set; } = "profile_field";
    public string ProfileFieldMapping { get; set; } = "";
    public decimal ConfidenceThreshold { get; set; } = 0.75m;
    public int UseCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class JobLead
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public string Platform { get; set; } = "linkedin";
    public string ApplyPlatform { get; set; } = "unknown";
    public string ApplySurface { get; set; } = "unknown";
    public string ApplyUrl { get; set; } = "";
    public string AutomationSupport { get; set; } = "unknown";
    public string AutomationReason { get; set; } = "";
    public string Status { get; set; } = "";
    public int Score { get; set; }
    public string ScoreReason { get; set; } = "";
    public string? FilteredReason { get; set; }
    public bool IsRemote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record ApplicationJob(Guid Id, string State)
{
    public Guid? ResumeVersionId { get; init; }
    public Guid? ResumeExportId { get; init; }
    public string ResumeFilePath { get; init; } = "";
}

public sealed record AgentTaskResponse(
    ApplicationJob Task, JobLead Lead,
    CandidateProfile? Profile, List<ReusableAnswer> Answers)
{
    public string? ResolveResumePath()
    {
        if (!string.IsNullOrWhiteSpace(Task.ResumeFilePath))
            return Task.ResumeFilePath;

        return string.IsNullOrWhiteSpace(Profile?.ResumePath) ? null : Profile.ResumePath;
    }
}

public sealed record ApplySurfaceRequest(string ApplyPlatform, string ApplySurface, string ApplyUrl, string AutomationSupport, string AutomationReason);

public sealed record QueueExtensionCommandRequest(Guid SessionId, string Type, string ElementId = "", string Value = "", string ReasonCode = "");

public sealed record ExtensionStatusResponse(Guid? SessionId, string Status, string CurrentUrl, bool IsSupportedPage, string Browser, string ExtensionVersion, string LastError, DateTimeOffset? LastHeartbeatAt, DateTimeOffset? LastObservedAt);

public sealed record ExtensionCommandResponse(Guid Id, string Type, string ElementId, string Value, string ReasonCode);

public sealed record ExtensionCommandStatusResponse(Guid Id, string Status, string ResultJson, string Error, DateTimeOffset? ClaimedAt, DateTimeOffset? CompletedAt);

public sealed record AnswerDraftResponse(string Text, bool UsedKimi, string Model);

public sealed record PageStateRequest(string Url, string Title, string VisibleText);

public sealed record PageStateResponse(string State, string Reason, decimal Confidence);

public sealed record BrowserActionPlanRequest(
    string Objective,
    string PageState,
    string ObservationJson,
    string? JobContext,
    Guid? ApplicationJobId = null,
    int Attempt = 1,
    string? PreviousFailure = null,
    string? DecisionMemory = null);

public sealed record BrowserActionPlanResponse(
    string ActionType, string ElementId, string Value,
    string Reason, decimal Confidence, bool RequiresCheckpoint);

public sealed record StepValidationRequest(
    string ActionJson, string BeforeObservationJson,
    string AfterObservationJson, string? JobContext);

public sealed record StepValidationResponse(
    bool Success, string Reason, decimal Confidence, bool RequiresCheckpoint);

public sealed record ResolveAgentAnswerRequest(
    Guid ApplicationJobId, string QuestionText,
    string FieldContext, string JobContext, string FieldType);

public sealed record ResolveAgentAnswerResponse(
    string AnswerValue, string Source, decimal Confidence,
    string Reason, bool RequiresCheckpoint);

public sealed record AgentRunRequest(
    Guid? ApplicationJobId, string AgentName, string Mode, string PolicyMode);

public sealed record AgentRunCompleteRequest(string State);

public sealed record AgentRunResponse(Guid Id, string State);

public sealed record AgentStepRequest(
    Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber,
    string AgentName, string PageState, string ActionType,
    string TargetLabel, string Result, string EvidencePath, decimal Confidence);

public sealed record AgentTraceStepRequest(
    Guid? AgentRunId,
    Guid? ApplicationJobId,
    int StepNumber,
    string ObjectiveKind,
    string ObjectiveJson,
    string Platform,
    string Module,
    string Phase,
    string BeforeFingerprint,
    string AfterFingerprint,
    string Planner,
    string ProposalJson,
    string RiskJson,
    string GuardVerdictJson,
    string ExecutionJson,
    string OutcomeJson,
    string RecoveryJson,
    string ContentSafetyJson,
    string EvidencePath,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record AgentTraceStepResponse(Guid Id);

public sealed record AgentCheckpointRequest(
    Guid? ApplicationJobId,
    Guid? AgentRunId,
    Guid? TraceStepId,
    string ReasonCode,
    string Stage,
    string FieldLabel,
    string Detail,
    string SuggestedUserAction,
    string ResumeMode);

public sealed class JobFilter
{
    public string[] AllowedSeniorities { get; init; } = [];
    public string[] BlockedSeniorities { get; init; } = [];
    public string[] AllowedRoleKeywords { get; init; } = [];
    public string[] BlockedRoleKeywords { get; init; } = [];
    public string[] AllowedWorkplaceTypes { get; init; } = [];
    public string[] BlockedWorkplaceTypes { get; init; } = [];
    public string[] AllowedApplyTypes { get; init; } = [];
    public int MinScore { get; init; }
    public bool AutoIgnoreLowScore { get; init; }

    public JobFilter()
    {
    }

    public JobFilter(
        string[] allowedSeniorities, string[] blockedSeniorities,
        string[] allowedWorkplaceTypes, string[] blockedWorkplaceTypes,
        string[] allowedApplyTypes, int minScore, bool autoIgnoreLowScore)
        : this(allowedSeniorities, blockedSeniorities, [], [], allowedWorkplaceTypes, blockedWorkplaceTypes, allowedApplyTypes, minScore, autoIgnoreLowScore)
    {
    }

    [JsonConstructor]
    public JobFilter(
        string[] allowedSeniorities, string[] blockedSeniorities,
        string[] allowedRoleKeywords, string[] blockedRoleKeywords,
        string[] allowedWorkplaceTypes, string[] blockedWorkplaceTypes,
        string[] allowedApplyTypes, int minScore, bool autoIgnoreLowScore)
    {
        AllowedSeniorities = allowedSeniorities ?? [];
        BlockedSeniorities = blockedSeniorities ?? [];
        AllowedRoleKeywords = allowedRoleKeywords ?? [];
        BlockedRoleKeywords = blockedRoleKeywords ?? [];
        AllowedWorkplaceTypes = allowedWorkplaceTypes ?? [];
        BlockedWorkplaceTypes = blockedWorkplaceTypes ?? [];
        AllowedApplyTypes = allowedApplyTypes ?? [];
        MinScore = minScore;
        AutoIgnoreLowScore = autoIgnoreLowScore;
    }
}

public sealed record ProfileValidationResult(bool Valid, bool Critical, List<ProfileValidationIssue> Issues);

public sealed record ProfileValidationIssue(string FieldName, string ErrorMessage, bool IsCritical);

public sealed record ResolvedAnswer(string AnswerValue, string Source, decimal Confidence, string Reason);

public sealed record ApplicationFieldRequest(
    Guid ApplicationJobId, string FieldKey, string Label,
    string FieldType, string ValueSource, decimal Confidence);

public sealed record ApplicationAnswerRequest(
    Guid ApplicationJobId, string QuestionText, string AnswerValue,
    string Source, string ProviderModel, decimal Confidence);

public sealed record SiteWorkflowMemoryRequest(
    string Domain, string PatternKey, string ActionJson, bool Success);

public sealed class AgentLearning
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string Domain { get; set; } = "";
    public string LearningType { get; set; } = "";
    public string PatternKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string TriggerJson { get; set; } = "{}";
    public string ActionJson { get; set; } = "{}";
    public string EvidenceJson { get; set; } = "{}";
    public string Status { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record AgentLearningRequest(
    Guid? ApplicationJobId, string Domain, string LearningType,
    string PatternKey, string Title, string Summary,
    string TriggerJson, string ActionJson, string EvidenceJson,
    string RiskLevel);

public sealed record AgentLearningOutcomeRequest(string PatternKey, bool Success, Guid? ApplicationJobId, string? Detail);

public sealed record AgentPolicy(
    string AutoSubmitMode, bool IsPaused,
    int MaxApplicationsPerRun, int MaxApplicationsPerDay,
    string[] AllowedDomains, string[] BlockedDomains,
    string ExecutionTarget = "extension");

public sealed record ElementBounds(int X, int Y, int Width, int Height);

public sealed record ElementCandidate(
    string ElementId, string Tag, string Type, string Role,
    string Label, string Text, string Value, bool Required,
    bool Disabled, bool Checked, string Href,
    List<string> Options, ElementBounds Bounds,
    string Placeholder = "", string Pattern = "", string InputMode = "",
    string AutoComplete = "", string DataMask = "", string Title = "",
    string Name = "", string? FieldGroupId = null, int? WizardStep = null,
    string? DescribedBy = null)
{
    public bool IsFile => Type.Equals("file", StringComparison.OrdinalIgnoreCase);
    public bool IsField => Tag is "input" or "textarea" or "select" || Role is "textbox" or "combobox" or "radio" or "checkbox" || IsSelectLikeButton;
    public bool IsSelect => Tag is "select" || Role is "combobox" || IsSelectLikeButton;
    public bool IsChoice => Type is "radio" or "checkbox" || Role is "radio" or "checkbox";
    public bool IsTextEntry => Tag is "textarea" || (Tag == "input" && Type is not ("radio" or "checkbox" or "file" or "submit" or "button"));
    public bool IsButtonLike => Tag is "button" or "a" || Role is "button";
    public bool IsDisabledState => Disabled;
    public bool IsSensitive => IsSensitiveSearchText(SearchText);
    public string TypeOrTag => string.IsNullOrWhiteSpace(Type) ? Tag : Type;
    public string TypeOrRole => !string.IsNullOrWhiteSpace(Type) ? Type : (!string.IsNullOrWhiteSpace(Role) ? Role : Tag);
    public string SearchText => $"{Label} {Text} {Type} {Role} {string.Join(' ', Options)}".Trim();
    public string SearchTextWithHints => $"{Label} {Placeholder} {DescribedBy} {Title} {Name} {Text} {Type} {Role}".Trim();
    private bool IsSelectLikeButton =>
        Tag is "button" or "div" or "span" &&
        AgentText.ContainsAny($"{Label} {Text} {Placeholder} {Title} {Name}", [
            "selecionar opcao", "selecionar opção", "select an option", "selecionar uma opcao", "selecionar uma opção"
        ]);

    private static bool IsSensitiveSearchText(string text) =>
        AgentText.ContainsAny(text, [
            "visa", "visto", "authorization", "autorizacao", "autorização",
            "disability", "deficiencia", "race", "gender", "criminal"
        ]) ||
        Regex.IsMatch(text, @"(?<![A-Za-z0-9])cpf(?![A-Za-z0-9])", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(text, @"(?<![A-Za-z0-9])rg(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
}

public sealed record PageObservation(string Url, string Title, string VisibleText, List<ElementCandidate> Elements, bool HasModal = false, bool CanScrollDown = false)
{
    public string Fingerprint()
    {
        var compact = string.Join('|', Elements.Take(35).Select(e => $"{e.Tag}:{e.Type}:{e.Role}:{e.Label}:{e.Value}:{e.Checked}"));
        return $"{Url.Split('#')[0]}::{CanScrollDown}:{compact}".GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed record BrowserAction(string Type, string ElementId, string Value, string Label, string Reason, decimal Confidence)
{
    public static BrowserAction Checkpoint(string elementId, string label, string reason) =>
        new("checkpoint", elementId, "", label, reason, 0m);
}

public sealed record ActionExecutionResult(bool Success, string Message, string? CheckpointCode)
{
    public static ActionExecutionResult Ok(string message) => new(true, message, null);
    public static ActionExecutionResult Fail(string checkpointCode, string message) => new(false, message, checkpointCode);
}

public sealed record PlatformCredential(string Platform, string AccountEmail, string Password);

internal static class AgentText
{
    public static bool ContainsAny(string text, IEnumerable<string> needles)
    {
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        return needles.Any(needle => normalized.Contains(RemoveDiacritics(needle).ToLowerInvariant()));
    }

    public static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(ch =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
