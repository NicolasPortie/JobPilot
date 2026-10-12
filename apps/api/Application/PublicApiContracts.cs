using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record ExecutionEventResponse(
    Guid Id,
    Guid? ApplicationJobId,
    string EventType,
    string Detail,
    DateTimeOffset OccurredAt)
{
    public static ExecutionEventResponse From(ExecutionEvent executionEvent) =>
        new(
            executionEvent.Id,
            executionEvent.ApplicationJobId,
            executionEvent.EventType,
            executionEvent.Detail,
            executionEvent.OccurredAt);
}

public sealed record JobFilterResponse(
    Guid Id,
    string[] AllowedSeniorities,
    string[] BlockedSeniorities,
    string[] AllowedRoleKeywords,
    string[] BlockedRoleKeywords,
    string[] AllowedWorkplaceTypes,
    string[] BlockedWorkplaceTypes,
    string[] AllowedApplyTypes,
    int MinScore,
    bool AutoIgnoreLowScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static JobFilterResponse From(JobFilter filter) =>
        new(
            filter.Id,
            filter.AllowedSeniorities,
            filter.BlockedSeniorities,
            filter.AllowedRoleKeywords,
            filter.BlockedRoleKeywords,
            filter.AllowedWorkplaceTypes,
            filter.BlockedWorkplaceTypes,
            filter.AllowedApplyTypes,
            filter.MinScore,
            filter.AutoIgnoreLowScore,
            filter.CreatedAt,
            filter.UpdatedAt);
}

public sealed record ReusableAnswerRequest(
    string QuestionKey,
    string QuestionText,
    string AnswerValue,
    string Category);

public sealed record ReusableAnswerResponse(
    Guid Id,
    string QuestionKey,
    string QuestionText,
    string AnswerValue,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ReusableAnswerResponse From(ReusableAnswer answer) =>
        new(
            answer.Id,
            answer.QuestionKey,
            answer.QuestionText,
            answer.AnswerValue,
            answer.Category,
            answer.CreatedAt,
            answer.UpdatedAt);
}

public sealed record QuestionPatternResponse(
    Guid Id,
    string PatternKey,
    string Category,
    string[] Tags,
    string[] QuestionTemplates,
    string AnswerTemplate,
    string ValueSource,
    string ProfileFieldMapping,
    decimal ConfidenceThreshold,
    int UseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static QuestionPatternResponse From(QuestionPattern pattern) =>
        new(
            pattern.Id,
            pattern.PatternKey,
            pattern.Category,
            pattern.Tags,
            pattern.QuestionTemplates,
            pattern.AnswerTemplate,
            pattern.ValueSource,
            pattern.ProfileFieldMapping,
            pattern.ConfidenceThreshold,
            pattern.UseCount,
            pattern.CreatedAt,
            pattern.UpdatedAt);
}

public sealed record CandidateProfileRequest(
    string FullName,
    string Email,
    string Phone,
    string City,
    string State,
    string Country,
    string LinkedinUrl,
    string GithubUrl,
    string PortfolioUrl,
    string ProfessionalSummary,
    string[] Skills,
    string TargetRole,
    string Seniority,
    string SalaryExpectation);

public sealed record CandidateProfileResponse(
    Guid? Id,
    string FullName,
    string Email,
    string Phone,
    string City,
    string State,
    string Country,
    string LinkedinUrl,
    string GithubUrl,
    string PortfolioUrl,
    string ProfessionalSummary,
    string[] Skills,
    string TargetRole,
    string Seniority,
    string SalaryExpectation,
    DateTimeOffset? UpdatedAt)
{
    public static CandidateProfileResponse Empty() =>
        new(null, "", "", "", "", "", "Brasil", "", "", "", "", [], "", "junior", "", null);

    public static CandidateProfileResponse From(CandidateProfile profile) =>
        new(
            profile.Id,
            profile.FullName,
            profile.Email,
            profile.Phone,
            profile.City,
            profile.State,
            profile.Country,
            profile.LinkedinUrl,
            profile.GithubUrl,
            profile.PortfolioUrl,
            profile.ProfessionalSummary,
            profile.Skills,
            profile.TargetRole,
            profile.Seniority,
            profile.SalaryExpectation,
            profile.UpdatedAt);
}

/// <summary>
/// Perfil mínimo necessário pelo runtime legado do agente. Não é usado pelo
/// produto web e não deve ser reutilizado em endpoints públicos de perfil.
/// </summary>
public sealed record AgentCandidateProfileResponse(
    Guid? Id,
    string FullName,
    string Cpf,
    string Email,
    string Phone,
    string City,
    string State,
    string Country,
    string LinkedinUrl,
    string GithubUrl,
    string PortfolioUrl,
    string ProfessionalSummary,
    string[] Skills,
    string TargetRole,
    string Seniority,
    string SalaryExpectation,
    string ResumePath,
    DateTimeOffset? UpdatedAt)
{
    public static AgentCandidateProfileResponse From(CandidateProfile profile) =>
        new(
            profile.Id,
            profile.FullName,
            profile.Cpf,
            profile.Email,
            profile.Phone,
            profile.City,
            profile.State,
            profile.Country,
            profile.LinkedinUrl,
            profile.GithubUrl,
            profile.PortfolioUrl,
            profile.ProfessionalSummary,
            profile.Skills,
            profile.TargetRole,
            profile.Seniority,
            profile.SalaryExpectation,
            profile.ResumePath,
            profile.UpdatedAt);
}

public sealed record ProfileValidationResult(
    bool Valid,
    bool Critical,
    IReadOnlyList<ProfileValidationIssue> Issues);

public sealed record ProfileCompletionResponse(
    int Percent,
    int Passed,
    int Total);

public sealed record RetentionPolicyResponse(
    int ResumeOriginalsDays,
    int ResumeExportsDays,
    int AiUsageLogsDays,
    int JobSourceFilesDays);

public sealed record ProfileValidationRuleResponse(
    Guid Id,
    string FieldName,
    string ValidationType,
    string ValidationValue,
    string ErrorMessage,
    bool IsCritical,
    DateTimeOffset CreatedAt)
{
    public static ProfileValidationRuleResponse From(ProfileValidationRule rule) =>
        new(
            rule.Id,
            rule.FieldName,
            rule.ValidationType,
            rule.ValidationValue,
            rule.ErrorMessage,
            rule.IsCritical,
            rule.CreatedAt);
}
