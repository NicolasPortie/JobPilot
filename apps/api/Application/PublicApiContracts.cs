using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

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
