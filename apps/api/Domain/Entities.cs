using System.Security.Claims;

namespace JobPilot.Api.Domain;

public static class JobStatuses
{
    public const string Found = "found";
    public const string Scored = "scored";
    public const string Shortlisted = "shortlisted";
    public const string QueuedToApply = "queued_to_apply";
    public const string Opening = "opening";
    public const string Filling = "filling";
    public const string NeedsReview = "needs_review";
    public const string ReadyForSubmit = "ready_for_submit";
    public const string Applied = "applied";
    public const string Failed = "failed";
    public const string Ignored = "ignored";
    public const string ManualOnly = "manual_only";
}

public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CandidateProfile
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
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(CandidateProfile input)
    {
        FullName = input.FullName;
        Cpf = input.Cpf;
        Email = input.Email;
        Phone = input.Phone;
        City = input.City;
        Country = input.Country;
        LinkedinUrl = input.LinkedinUrl;
        GithubUrl = input.GithubUrl;
        PortfolioUrl = input.PortfolioUrl;
        ProfessionalSummary = input.ProfessionalSummary;
        Skills = input.Skills;
        TargetRole = input.TargetRole;
        Seniority = input.Seniority;
        SalaryExpectation = input.SalaryExpectation;
        ResumePath = input.ResumePath;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class ResumeImport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SourceFileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string ExtractionMethod { get; set; } = "";
    public string Classification { get; set; } = "";
    public int TextQualityScore { get; set; }
    public decimal Confidence { get; set; }
    public bool RequiresOcr { get; set; }
    public bool RequiresReview { get; set; }
    public int Pages { get; set; }
    public int PagesWithNativeText { get; set; }
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public string RawText { get; set; } = "";
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ResumeDocument
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ResumeImportId { get; set; }
    public string Title { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string Status { get; set; } = "draft";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ResumeVersion> Versions { get; set; } = [];
}

public class ResumeVersion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeDocumentId { get; set; }
    public Guid? JobLeadId { get; set; }
    public string Title { get; set; } = "";
    public string TargetCompany { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public string Status { get; set; } = "draft";
    public int AtsScore { get; set; }
    public string AtsReportJson { get; set; } = "{}";
    public string PlainText { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ResumeDocument? Document { get; set; }
    public List<ResumeSection> Sections { get; set; } = [];
}

public class ResumeSection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public string SectionType { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public string ContentJson { get; set; } = "{}";
    public ResumeVersion? Version { get; set; }
}

public class ResumeExport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public string Format { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ParseText { get; set; } = "";
    public string ParseWarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ResumeTailoringRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid? JobLeadId { get; set; }
    public string ProviderModel { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

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

public class JobTarget
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? JobLeadId { get; set; }
    public string SourceKind { get; set; } = "manual_text";
    public string SourceText { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Seniority { get; set; } = "unknown";
    public string Workplace { get; set; } = "unknown";
    public string Summary { get; set; } = "";
    public string Status { get; set; } = "organized";
    public decimal Confidence { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<JobRequirement> Requirements { get; set; } = [];
}

public class JobRequirement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid JobTargetId { get; set; }
    public string Category { get; set; } = "";
    public string Label { get; set; } = "";
    public string Importance { get; set; } = "context";
    public string EvidenceText { get; set; } = "";
    public string NormalizedKeyword { get; set; } = "";
    public string[] ExactPhrases { get; set; } = [];
    public int SortOrder { get; set; }
    public JobTarget? JobTarget { get; set; }
}

public class ResumeEvidenceMapRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid JobTargetId { get; set; }
    public string Status { get; set; } = "ready";
    public int EvidenceScore { get; set; }
    public decimal Confidence { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ResumeRequirementEvidence> Items { get; set; } = [];
}

public class ResumeRequirementEvidence
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeEvidenceMapRunId { get; set; }
    public Guid JobRequirementId { get; set; }
    public string RequirementLabel { get; set; } = "";
    public string RequirementCategory { get; set; } = "";
    public string RequirementImportance { get; set; } = "";
    public string MatchStatus { get; set; } = "unknown";
    public string EvidenceText { get; set; } = "";
    public string EvidenceSource { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Confidence { get; set; }
    public int SortOrder { get; set; }
    public ResumeEvidenceMapRun? Run { get; set; }
}

public class SavedSearch
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string Location { get; set; } = "Brazil";
    public string Workplace { get; set; } = "remote";
    public string Seniority { get; set; } = "junior";
    public int PostedWithinDays { get; set; } = 7;
    public int LimitPerRun { get; set; } = 25;
    public string Platform { get; set; } = "linkedin";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(SavedSearch input)
    {
        Name = input.Name;
        Keywords = input.Keywords;
        Location = input.Location;
        Workplace = input.Workplace;
        Seniority = input.Seniority;
        PostedWithinDays = input.PostedWithinDays;
        LimitPerRun = input.LimitPerRun;
        IsActive = input.IsActive;
    }
}

public class JobLead
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
    public int Score { get; set; }
    public string ScoreReason { get; set; } = "";
    public string Status { get; set; } = JobStatuses.Found;
    public bool IsRemote { get; set; }
    public string FilteredReason { get; set; } = "";
    public DateTimeOffset FoundAt { get; set; } = DateTimeOffset.UtcNow;
}

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

public class AiProvider
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string ProviderType { get; set; } = "kimi";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiFormat { get; set; } = "anthropic-messages";
    public string EncryptedApiKey { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(AiProvider input)
    {
        Name = input.Name;
        ProviderType = input.ProviderType;
        BaseUrl = input.BaseUrl;
        Model = input.Model;
        ApiFormat = input.ApiFormat;
        IsActive = input.IsActive;
        IsDefault = input.IsDefault;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

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

public class AgentRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string AgentName { get; set; } = "";
    public string Mode { get; set; } = "";
    public string State { get; set; } = "running";
    public string PolicyMode { get; set; } = "conservative";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public class AgentStep
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public int StepNumber { get; set; }
    public string AgentName { get; set; } = "";
    public string PageState { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string TargetLabel { get; set; } = "";
    public string Result { get; set; } = "";
    public string EvidencePath { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AgentTraceStep
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public int StepNumber { get; set; }
    public string ObjectiveKind { get; set; } = "";
    public string ObjectiveJson { get; set; } = "{}";
    public string Platform { get; set; } = "";
    public string Module { get; set; } = "";
    public string Phase { get; set; } = "";
    public string BeforeFingerprint { get; set; } = "";
    public string AfterFingerprint { get; set; } = "";
    public string Planner { get; set; } = "";
    public string ProposalJson { get; set; } = "{}";
    public string RiskJson { get; set; } = "{}";
    public string GuardVerdictJson { get; set; } = "{}";
    public string ExecutionJson { get; set; } = "{}";
    public string OutcomeJson { get; set; } = "{}";
    public string RecoveryJson { get; set; } = "{}";
    public string ContentSafetyJson { get; set; } = "{}";
    public string EvidencePath { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ApplicationField
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationJobId { get; set; }
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public string FieldType { get; set; } = "";
    public string ValueSource { get; set; } = "";
    public decimal Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

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

public class SiteWorkflowMemory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Domain { get; set; } = "";
    public string PatternKey { get; set; } = "";
    public string ActionJson { get; set; } = "";
    public int SuccessCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class AgentLearningStatuses
{
    public const string Candidate = "candidate";
    public const string AutoApproved = "auto_approved";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Disabled = "disabled";
}

public static class AgentLearningTypes
{
    public const string Technical = "technical";
    public const string Format = "format";
    public const string Answer = "answer";
    public const string Sensitive = "sensitive";
    public const string Workflow = "workflow";
}

public static class AgentLearningRiskLevels
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
}

public class AgentLearning
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string Domain { get; set; } = "";
    public string LearningType { get; set; } = AgentLearningTypes.Technical;
    public string PatternKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string TriggerJson { get; set; } = "{}";
    public string ActionJson { get; set; } = "{}";
    public string EvidenceJson { get; set; } = "{}";
    public string Status { get; set; } = AgentLearningStatuses.Candidate;
    public string RiskLevel { get; set; } = AgentLearningRiskLevels.Low;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    public bool IsActive => Status is AgentLearningStatuses.Approved or AgentLearningStatuses.AutoApproved;
}

public class AgentPolicy
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AutoSubmitMode { get; set; } = "conservative";
    public string ExecutionTarget { get; set; } = ExecutionTargets.Extension;
    public int MaxApplicationsPerRun { get; set; } = 10;
    public int MaxApplicationsPerDay { get; set; } = 50;
    public string[] AllowedDomains { get; set; } = [];
    public string[] BlockedDomains { get; set; } = [];
    public bool IsPaused { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(AgentPolicy input)
    {
        AutoSubmitMode = input.AutoSubmitMode;
        ExecutionTarget = ExecutionTargets.Normalize(input.ExecutionTarget);
        MaxApplicationsPerRun = input.MaxApplicationsPerRun;
        MaxApplicationsPerDay = input.MaxApplicationsPerDay;
        AllowedDomains = input.AllowedDomains;
        BlockedDomains = input.BlockedDomains;
        IsPaused = input.IsPaused;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public static class ExecutionTargets
{
    public const string Extension = "extension";
    public const string LocalDedicatedBrowser = "local_dedicated_browser";
    public const string RemoteBrowser = "remote_browser";
    public const string ServerConnector = "server_connector";

    public static string Normalize(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace("-", "_");
        return normalized switch
        {
            "" => Extension,
            "playwright" or "local_browser" or "local_dedicated_browser" => LocalDedicatedBrowser,
            "remote" or "remote_browser" => RemoteBrowser,
            "server" or "server_connector" => ServerConnector,
            _ => Extension
        };
    }

    public static bool IsImplemented(string value) =>
        value is Extension or LocalDedicatedBrowser;
}

public class AgentDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class BrowserExtensionSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string InstallationId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string Status { get; set; } = "paired";
    public string CurrentUrl { get; set; } = "";
    public bool IsSupportedPage { get; set; }
    public string Browser { get; set; } = "chrome";
    public string ExtensionVersion { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastObservedAt { get; set; }
}

public class BrowserExtensionCommandJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BrowserExtensionSessionId { get; set; }
    public string Type { get; set; } = "";
    public string ElementId { get; set; } = "";
    public string Value { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string ResultJson { get; set; } = "{}";
    public string Error { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class ApplicationRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string State { get; set; } = "queued";
    public int TotalJobs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ApplicationJob> Jobs { get; set; } = [];
}

public class ApplicationJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationRunId { get; set; }
    public ApplicationRun? ApplicationRun { get; set; }
    public Guid JobLeadId { get; set; }
    public JobLead? JobLead { get; set; }
    public Guid? ResumeVersionId { get; set; }
    public ResumeVersion? ResumeVersion { get; set; }
    public Guid? ResumeExportId { get; set; }
    public ResumeExport? ResumeExport { get; set; }
    public string ResumeFilePath { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string State { get; set; } = JobStatuses.QueuedToApply;
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ExecutionEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public string EventType { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Checkpoint
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApplicationJobId { get; set; }
    public Guid? AgentRunId { get; set; }
    public Guid? TraceStepId { get; set; }
    public string ReasonCode { get; set; } = "";
    public string Stage { get; set; } = "";
    public string FieldLabel { get; set; } = "";
    public string Detail { get; set; } = "";
    public string ResolutionStatus { get; set; } = "open";
    public string? ResolutionValue { get; set; }
    public string SuggestedUserAction { get; set; } = "";
    public string ResumeMode { get; set; } = "reobserve";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public class QuestionPattern
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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(QuestionPattern input)
    {
        PatternKey = input.PatternKey;
        Category = input.Category;
        Tags = input.Tags;
        QuestionTemplates = input.QuestionTemplates;
        AnswerTemplate = input.AnswerTemplate;
        ValueSource = input.ValueSource;
        ProfileFieldMapping = input.ProfileFieldMapping;
        ConfidenceThreshold = input.ConfidenceThreshold;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class JobFilter
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string[] AllowedSeniorities { get; set; } = ["junior"];
    public string[] BlockedSeniorities { get; set; } = ["senior", "sênior", "sr", "sr.", "pleno", "pl", "staff", "principal", "lead", "manager", "coordenador"];
    public string[] AllowedRoleKeywords { get; set; } = ["asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect"];
    public string[] BlockedRoleKeywords { get; set; } = ["suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados"];
    public string[] AllowedWorkplaceTypes { get; set; } = ["remote"];
    public string[] BlockedWorkplaceTypes { get; set; } = ["hybrid", "híbrido", "hibrido", "onsite", "on-site", "presencial"];
    public string[] AllowedApplyTypes { get; set; } = ["linkedin_easy_apply", "inhire"];
    public int MinScore { get; set; } = 30;
    public bool AutoIgnoreLowScore { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(JobFilter input)
    {
        AllowedSeniorities = input.AllowedSeniorities;
        BlockedSeniorities = input.BlockedSeniorities;
        AllowedRoleKeywords = input.AllowedRoleKeywords ?? AllowedRoleKeywords;
        BlockedRoleKeywords = input.BlockedRoleKeywords ?? BlockedRoleKeywords;
        AllowedWorkplaceTypes = input.AllowedWorkplaceTypes;
        BlockedWorkplaceTypes = input.BlockedWorkplaceTypes;
        AllowedApplyTypes = input.AllowedApplyTypes;
        MinScore = input.MinScore;
        AutoIgnoreLowScore = input.AutoIgnoreLowScore;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class ProfileValidationRule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FieldName { get; set; } = "";
    public string ValidationType { get; set; } = "not_empty";
    public string ValidationValue { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public bool IsCritical { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string Email);
public record StatusRequest(string Status);
public record PairDeviceRequest(string Name);
public record PairDeviceResponse(Guid DeviceId, string Token);
public record PairExtensionRequest(string Name);
public record PairExtensionResponse(Guid SessionId, string Token);
public record LocalExtensionPairRequest(string Name, string Browser, string ExtensionVersion, string InstallationId = "");
public record ExtensionHeartbeatRequest(string Token, string CurrentUrl, bool IsSupportedPage, string Browser, string ExtensionVersion, string LastError = "");
public record ExtensionStatusResponse(Guid? SessionId, string Status, string CurrentUrl, bool IsSupportedPage, string Browser, string ExtensionVersion, string LastError, DateTimeOffset? LastHeartbeatAt, DateTimeOffset? LastObservedAt);
public record QueueExtensionCommandRequest(Guid SessionId, string Type, string ElementId = "", string Value = "", string ReasonCode = "");
public record ExtensionCommandResponse(Guid Id, string Type, string ElementId, string Value, string ReasonCode);
public record ExtensionCommandStatusResponse(Guid Id, string Status, string ResultJson, string Error, DateTimeOffset? ClaimedAt, DateTimeOffset? CompletedAt);
public record ExtensionCommandPollRequest(string Token, string CurrentUrl, bool IsSupportedPage, string Browser, string ExtensionVersion);
public record ExtensionCommandResultRequest(string Token, bool Success, string ResultJson, string Error = "");
public record AutomationDiscoverRequest(string Keywords, string Location, int Limit);
public record AutomationApplyRequest(bool AutoSubmit, int MaxApplications);
public record CreateRunRequest(string Name, Guid[] JobLeadIds);
public record ResolveCheckpointRequest(string Action, string? Value);
public record AnswerDraftRequest(string QuestionText, string? JobContext);
public record QuestionPatternRequest(string PatternKey, string Category, string[] Tags, string[] QuestionTemplates, string AnswerTemplate, string ValueSource, string ProfileFieldMapping, decimal ConfidenceThreshold);
public record JobFilterRequest(string[] AllowedSeniorities, string[] BlockedSeniorities, string[] AllowedRoleKeywords, string[] BlockedRoleKeywords, string[] AllowedWorkplaceTypes, string[] BlockedWorkplaceTypes, string[] AllowedApplyTypes, int MinScore, bool AutoIgnoreLowScore);
public record ApplySurfaceRequest(string ApplyPlatform, string ApplySurface, string ApplyUrl, string AutomationSupport, string AutomationReason);
public record ProfileValidationRuleRequest(string FieldName, string ValidationType, string ValidationValue, string ErrorMessage, bool IsCritical);
public record ProfileValidationIssue(string FieldName, string ErrorMessage, bool IsCritical);
public record ResolvedAnswer(string AnswerValue, string Source, decimal Confidence, string Reason);
public class PlatformCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Platform { get; set; } = ""; // e.g. "linkedin", "indeed"
    public string AccountEmail { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(PlatformCredential input)
    {
        Platform = input.Platform;
        AccountEmail = input.AccountEmail;
        IsActive = input.IsActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public record PlatformCredentialRequest(string Platform, string AccountEmail, string Password, bool IsActive);
public record PlatformCredentialResponse(Guid Id, string Platform, string AccountEmail, bool HasPassword, bool IsActive, DateTimeOffset UpdatedAt);
public record AgentPlatformCredentialResponse(string Platform, string AccountEmail, string Password);

public record AiProviderRequest(string Name, string ProviderType, string BaseUrl, string Model, string ApiFormat, string? ApiKey, bool IsActive, bool IsDefault);
public record AiProviderResponse(Guid Id, string Name, string ProviderType, string BaseUrl, string Model, string ApiFormat, bool HasApiKey, bool IsActive, bool IsDefault, DateTimeOffset UpdatedAt);
public record DiscoveredJobsRequest(List<DiscoveredJobDto> Jobs);
public record AgentStateRequest(string State, string? Reason);
public record BrowserActionPlanRequest(
    string Objective,
    string PageState,
    string ObservationJson,
    string? JobContext,
    Guid? ApplicationJobId = null,
    int Attempt = 1,
    string? PreviousFailure = null,
    string? DecisionMemory = null);
public record PageStateRequest(string Url, string Title, string VisibleText);
public record StepValidationRequest(string ActionJson, string BeforeObservationJson, string AfterObservationJson, string? JobContext);
public record BrowserActionPlanResponse(string ActionType, string ElementId, string Value, string Reason, decimal Confidence, bool RequiresCheckpoint);
public record PageStateResponse(string State, string Reason, decimal Confidence);
public record StepValidationResponse(bool Success, string Reason, decimal Confidence, bool RequiresCheckpoint);
public record JobFitReviewResponse(string Decision, int FitScore, string Reason, string DetectedSeniority, string DetectedWorkplace, string ApplyType, string[] Strengths, string[] Risks, decimal Confidence);
public record ResolveAgentAnswerRequest(Guid ApplicationJobId, string QuestionText, string FieldContext, string JobContext, string FieldType);
public record ResolveAgentAnswerResponse(string AnswerValue, string Source, decimal Confidence, string Reason, bool RequiresCheckpoint);
public record ProfileImportRequest(string? ResumePath, bool FetchPublicLinks = true, bool UseAi = true);
public record ProfileImportResponse(
    CandidateProfile Profile,
    int AnswersUpserted,
    string[] SkillsAdded,
    string[] LinksFetched,
    string[] Warnings,
    ResumeExtractionReport Extraction,
    string ExtractedText,
    ResumeProfileImportData ImportData);
public record ResumeExtractionReport(
    string SourceKind,
    string ExtractionMethod,
    string Classification,
    int TextQualityScore,
    decimal Confidence,
    bool RequiresOcr,
    bool RequiresReview,
    int Pages,
    int PagesWithNativeText,
    int CharacterCount,
    int WordCount,
    string[] Warnings);
public record ResumeProfileImportData(
    string FullName,
    string Email,
    string Phone,
    string City,
    string Country,
    string LinkedinUrl,
    string GithubUrl,
    string PortfolioUrl,
    string ProfessionalSummary,
    string[] Skills,
    string TargetRole,
    string Seniority,
    ResumeScreeningAnswer[] Answers);
public record ResumeScreeningAnswer(string QuestionKey, string QuestionText, string AnswerValue, string Category);
public record ResumeOcrStatus(bool Available, ResumeOcrCommandStatus Tesseract, ResumeOcrCommandStatus OcrMyPdf, string[] Notes);
public record ResumeOcrCommandStatus(string Command, bool Available, string Path);
public record ResumeImportSummary(Guid Id, string SourceFileName, string SourceKind, string ExtractionMethod, string Classification, int TextQualityScore, decimal Confidence, bool RequiresOcr, bool RequiresReview, int WordCount, DateTimeOffset CreatedAt);
public record ResumeDocumentSummary(Guid Id, string Title, string Status, string SourceFileName, string SourceKind, DateTimeOffset UpdatedAt, ResumeVersionSummary[] Versions);
public record ResumeVersionSummary(Guid Id, string Title, string Status, string TargetRole, string TargetCompany, int AtsScore, DateTimeOffset UpdatedAt);
public record ResumeVersionDetail(Guid Id, Guid ResumeDocumentId, string Title, string Status, string TargetRole, string TargetCompany, int AtsScore, string PlainText, ResumeSectionDto[] Sections);
public record ResumeSectionDto(Guid Id, string SectionType, string Title, int SortOrder, string ContentJson);
public record OrganizeJobTargetRequest(Guid? JobLeadId, string SourceKind, string? Text, string? Url, string? ImagePath = null, bool UseAi = true);
public record JobRequirementExtractionInput(string SourceText, string SourceUrl = "", string Title = "", string Company = "", string Location = "", AiInputAttachment[]? Attachments = null);
public record AiInputAttachment(string Type, string MediaType, string DataUrl = "", string FileName = "", string FileId = "");
public record JobRequirementExtractionResponse(string Title, string Company, string Location, string Seniority, string Workplace, string Summary, JobRequirementItem[] Requirements, string[] Warnings, decimal Confidence);
public record JobRequirementItem(string Category, string Label, string Importance, string EvidenceText, string NormalizedKeyword, string[] ExactPhrases);
public record JobTargetDetail(Guid Id, Guid? JobLeadId, string SourceKind, string SourceUrl, string Title, string Company, string Location, string Seniority, string Workplace, string Summary, string Status, decimal Confidence, string[] Warnings, JobRequirementDto[] Requirements, DateTimeOffset UpdatedAt);
public record JobRequirementDto(Guid Id, string Category, string Label, string Importance, string EvidenceText, string NormalizedKeyword, string[] ExactPhrases, int SortOrder);
public record BuildResumeEvidenceMapRequest(Guid ResumeVersionId, Guid JobTargetId);
public record ResumeEvidenceMapDetail(Guid Id, Guid ResumeVersionId, Guid JobTargetId, string Status, int EvidenceScore, decimal Confidence, string[] Warnings, ResumeRequirementEvidenceDto[] Items, DateTimeOffset CreatedAt);
public record ResumeRequirementEvidenceDto(Guid Id, Guid JobRequirementId, string RequirementLabel, string RequirementCategory, string RequirementImportance, string MatchStatus, string EvidenceText, string EvidenceSource, string Reason, decimal Confidence, int SortOrder);
public record TailorResumeRequest(Guid BaseResumeVersionId, Guid JobTargetId, Guid? EvidenceMapRunId = null, string Layout = "ats_classic", bool UseAi = true);
public record BuildGenericAtsResumeRequest(Guid BaseResumeVersionId, string Layout = "ats_reverse_chronological", bool UseAi = false);
public record ResumeLayoutOption(string Key, string Label, string Description, string AtsNotes, bool RecommendedForAts);
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
    ResumeVersionDetail Version);
public record ResumeScoreComponent(string Key, string Label, int Score, int MaxScore, string Status, string Reason);
public record ResumeSectionDiff(string SectionType, string Title, string Status, string BeforePreview, string AfterPreview, Guid? AfterSectionId = null);
public record ResumeReviewQuestion(Guid RequirementId, string RequirementLabel, string Category, string Importance, string QuestionText, string Reason);
public record TailoredResumeGenerationInput(
    string Layout,
    string CandidateJson,
    string JobTargetJson,
    string BaseResumeText,
    string DeterministicDraft,
    ResumeRequirementEvidenceDto[] EvidenceItems,
    string[] IncludedKeywords,
    string[] AdjacentKeywords,
    string[] ExcludedKeywords,
    string[] Checkpoints);
public record TailoredResumeGenerationResponse(string PlainText, string[] Warnings);
public record CreateResumeExportRequest(string Format = "txt", string Layout = "ats_classic");
public record ResumeExportResponse(Guid Id, Guid ResumeVersionId, string Format, string FilePath, string[] ParseWarnings, DateTimeOffset CreatedAt);
public record AgentRunRequest(Guid? ApplicationJobId, string AgentName, string Mode, string PolicyMode);
public record AgentRunCompleteRequest(string State);
public record AgentStepRequest(Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string AgentName, string PageState, string ActionType, string TargetLabel, string Result, string EvidencePath, decimal Confidence);
public record AgentTraceStepRequest(Guid? AgentRunId, Guid? ApplicationJobId, int StepNumber, string ObjectiveKind, string ObjectiveJson, string Platform, string Module, string Phase, string BeforeFingerprint, string AfterFingerprint, string Planner, string ProposalJson, string RiskJson, string GuardVerdictJson, string ExecutionJson, string OutcomeJson, string RecoveryJson, string ContentSafetyJson, string EvidencePath, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);
public record AgentTraceStepResponse(Guid Id);
public record AgentCheckpointRequest(Guid? ApplicationJobId, Guid? AgentRunId, Guid? TraceStepId, string ReasonCode, string Stage, string FieldLabel, string Detail, string SuggestedUserAction, string ResumeMode);
public record ApplicationFieldRequest(Guid ApplicationJobId, string FieldKey, string Label, string FieldType, string ValueSource, decimal Confidence);
public record ApplicationAnswerRequest(Guid ApplicationJobId, string QuestionText, string AnswerValue, string Source, string ProviderModel, decimal Confidence);
public record ApplicationJobResumeRequest(Guid ResumeVersionId, Guid? ResumeExportId = null);
public record ApplicationJobResumeResponse(Guid ApplicationJobId, Guid ResumeVersionId, Guid? ResumeExportId, string ResumeFilePath);
public record SiteWorkflowMemoryRequest(string Domain, string PatternKey, string ActionJson, bool Success);
public record AgentLearningRequest(Guid? ApplicationJobId, string Domain, string LearningType, string PatternKey, string Title, string Summary, string TriggerJson, string ActionJson, string EvidenceJson, string RiskLevel);
public record AgentLearningReviewRequest(string? Note);
public record AgentLearningOutcomeRequest(string PatternKey, bool Success, Guid? ApplicationJobId, string? Detail);

public record DiscoveredJobDto(string Title, string Company, string Location, string Description, string Url, bool IsRemote)
{
    public JobLead ToLead(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = Title,
        Company = Company,
        Location = Location,
        Description = Description,
        Url = Url,
        IsRemote = IsRemote,
        Platform = "linkedin",
        ApplyPlatform = "unknown",
        ApplySurface = "unknown",
        ApplyUrl = "",
        AutomationSupport = "unknown",
        AutomationReason = "",
        Status = JobStatuses.Found
    };
}

public record AgentTaskResponse(ApplicationJob Task, JobLead Lead, CandidateProfile? Profile, List<ReusableAnswer> Answers);

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("Missing user id claim."));

    public static Guid AgentUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("user_id");
        return userId is not null ? Guid.Parse(userId) : principal.UserId();
    }
}
