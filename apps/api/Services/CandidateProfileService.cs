using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;

namespace JobPilot.Api.Services;

public sealed class CandidateProfileService(
    IRepository<CandidateProfile> profiles,
    IRepository<ProfileSkillRecommendation> recommendations,
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

    /// <summary>Leva uma habilidade para o perfil (sem duplicar) e guarda de qual análise ela veio.</summary>
    public async Task<AddProfileSkillResponse> AddSkillAsync(Guid userId, AddProfileSkillRequest request)
    {
        var skill = request.Skill.Trim();
        var profile = await profiles.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
        if (profile is null)
        {
            profile = new CandidateProfile { Id = Guid.NewGuid(), UserId = userId };
            profiles.Add(profile);
        }

        var alreadyPresent = profile.Skills.Any(item => string.Equals(item.Trim(), skill, StringComparison.OrdinalIgnoreCase));
        if (!alreadyPresent)
        {
            profile.Skills = NormalizeSkills([.. profile.Skills, skill]).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            var previous = await recommendations.ListAsync(item => item.UserId == userId);
            foreach (var item in previous.Where(item => string.Equals(item.Skill, skill, StringComparison.OrdinalIgnoreCase)))
                recommendations.Remove(item);
            recommendations.Add(new ProfileSkillRecommendation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Skill = skill,
                Kind = request.Kind ?? "missing",
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Adicionada a partir de uma recomendação da análise." : request.Reason.Trim(),
                JobTitle = request.JobTitle?.Trim() ?? "",
                Company = request.Company?.Trim() ?? "",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await unitOfWork.SaveChangesAsync();
        }

        return new AddProfileSkillResponse(CandidateProfileResponse.From(profile), alreadyPresent);
    }

    public async Task<List<ProfileSkillRecommendationResponse>> ListSkillRecommendationsAsync(Guid userId) =>
        (await recommendations.ListAsync(item => item.UserId == userId))
        .OrderByDescending(item => item.CreatedAt)
        .Take(50)
        .Select(item => new ProfileSkillRecommendationResponse(item.Id, item.Skill, item.Kind, item.Reason, item.JobTitle, item.Company, item.CreatedAt))
        .ToList();

    public async Task<bool> RemoveSkillRecommendationAsync(Guid userId, Guid id)
    {
        var item = await recommendations.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId);
        if (item is null) return false;
        recommendations.Remove(item);
        await unitOfWork.SaveChangesAsync();
        return true;
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
            State = NormalizeState(request.State),
            Country = request.Country,
            LinkedinUrl = request.LinkedinUrl,
            GithubUrl = request.GithubUrl,
            PortfolioUrl = request.PortfolioUrl,
            ProfessionalSummary = request.ProfessionalSummary,
            Skills = NormalizeSkills(request.Skills),
            TargetRole = request.TargetRole,
            Seniority = request.Seniority,
            SalaryExpectation = request.SalaryExpectation,
            ResumePath = resumePath
        };

    /// <summary>UF em maiúsculas, só letras, no máximo duas.</summary>
    private static string NormalizeState(string? state) =>
        new(state?.Where(char.IsLetter).Take(2).Select(char.ToUpperInvariant).ToArray() ?? []);

    /// <summary>Remove espaços e itens vazios ou repetidos, mantendo a ordem informada.</summary>
    private static string[] NormalizeSkills(IEnumerable<string>? skills) =>
        (skills ?? [])
        .Select(skill => skill?.Trim() ?? "")
        .Where(skill => skill.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

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
