using FluentValidation;
using JobPilot.Api.Application;

namespace JobPilot.Api.Validators;

public sealed class ReusableAnswerRequestValidator : AbstractValidator<ReusableAnswerRequest>
{
    public ReusableAnswerRequestValidator()
    {
        RuleFor(x => x.QuestionKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AnswerValue).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
    }
}
