using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;

namespace JobPilot.Api.Services;

public sealed class CandidateProfileService(
    IRepository<CandidateProfile> profiles,
    IRepository<ProfileValidationRule> rules,
    IUnitOfWork unitOfWork,
    ProfileGuard guard)
{
    public async Task<CandidateProfileResponse> GetAsync(Guid userId)
    {
        var profile = await profiles.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        return profile is null ? CandidateProfileResponse.Empty() : CandidateProfileResponse.From(profile);
    }

    public async Task<CandidateProfileResponse> SaveAsync(Guid userId, CandidateProfileRequest request)
    {
        var profile = await profiles.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (profile is null)
        {
            profile = new CandidateProfile { Id = Guid.NewGuid(), UserId = userId };
            profiles.Add(profile);
        }

        // Identity data and ResumePath are internal profile state. They are
        // populated by resume import and must not be accepted from JSON saves.
        profile.Apply(ToEntity(request, profile.Cpf, profile.ResumePath));
        await unitOfWork.SaveChangesAsync();
        return CandidateProfileResponse.From(profile);
    }

    public async Task<ProfileValidationResult> ValidateAsync(Guid userId)
    {
        var issues = await guard.ValidateAsync(userId);
        return new ProfileValidationResult(
            issues.All(issue => !issue.IsCritical),
            issues.Any(issue => issue.IsCritical),
            issues);
    }

    public async Task<ProfileCompletionResponse> CompletionAsync(Guid userId)
    {
        var (passed, total) = await guard.CompletionAsync(userId);
        var percent = total == 0 ? 0 : (int)Math.Round(passed * 100.0 / total);
        return new ProfileCompletionResponse(percent, passed, total);
    }

    public async Task<List<ProfileValidationRuleResponse>> ListRulesAsync(Guid userId) =>
        (await rules.ListAsync(rule => rule.UserId == userId))
        .OrderBy(rule => rule.FieldName)
        .Select(ProfileValidationRuleResponse.From)
        .ToList();

    public async Task<ServiceResult<ProfileValidationRuleResponse>> CreateRuleAsync(Guid userId, ProfileValidationRuleRequest request)
    {
        var rule = BuildRule(userId, request);
        rules.Add(rule);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ProfileValidationRuleResponse>.Created(ProfileValidationRuleResponse.From(rule));
    }

    public async Task<ServiceResult<ProfileValidationRuleResponse>> UpdateRuleAsync(Guid userId, Guid id, ProfileValidationRuleRequest request)
    {
        var rule = await rules.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (rule is null) return ServiceResult<ProfileValidationRuleResponse>.NotFound();
        ApplyRule(rule, request);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ProfileValidationRuleResponse>.Ok(ProfileValidationRuleResponse.From(rule));
    }

    public async Task<ServiceResult<ProfileValidationRuleResponse>> DeleteRuleAsync(Guid userId, Guid id)
    {
        var rule = await rules.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (rule is null) return ServiceResult<ProfileValidationRuleResponse>.NotFound();
        rules.Remove(rule);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<ProfileValidationRuleResponse>.NoContent();
    }

    private static CandidateProfile ToEntity(CandidateProfileRequest request, string cpf, string resumePath) =>
        new()
        {
            FullName = request.FullName,
            Cpf = cpf,
            Email = request.Email,
            Phone = request.Phone,
            City = request.City,
            State = request.State,
            Country = request.Country,
            LinkedinUrl = request.LinkedinUrl,
            GithubUrl = request.GithubUrl,
            PortfolioUrl = request.PortfolioUrl,
            ProfessionalSummary = request.ProfessionalSummary,
            Skills = request.Skills,
            TargetRole = request.TargetRole,
            Seniority = request.Seniority,
            SalaryExpectation = request.SalaryExpectation,
            ResumePath = resumePath
        };

    private static ProfileValidationRule BuildRule(Guid userId, ProfileValidationRuleRequest request)
    {
        var rule = new ProfileValidationRule { Id = Guid.NewGuid(), UserId = userId };
        ApplyRule(rule, request);
        return rule;
    }

    private static void ApplyRule(ProfileValidationRule rule, ProfileValidationRuleRequest request)
    {
        rule.FieldName = request.FieldName;
        rule.ValidationType = request.ValidationType;
        rule.ValidationValue = request.ValidationValue;
        rule.ErrorMessage = request.ErrorMessage;
        rule.IsCritical = request.IsCritical;
    }
}
