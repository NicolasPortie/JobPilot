using FluentValidation;
using JobPilot.Api.Application;

namespace JobPilot.Api.Validators;

public sealed class CandidateProfileRequestValidator : AbstractValidator<CandidateProfileRequest>
{
    public CandidateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.LinkedinUrl).MaximumLength(500);
        RuleFor(x => x.GithubUrl).MaximumLength(500);
        RuleFor(x => x.PortfolioUrl).MaximumLength(500);
        RuleFor(x => x.TargetRole).MaximumLength(200);
        RuleFor(x => x.Seniority).MaximumLength(50);
        RuleFor(x => x.SalaryExpectation).MaximumLength(100);
    }
}
