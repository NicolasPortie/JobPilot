using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class QuickTailorRequestValidator : AbstractValidator<QuickTailorRequest>
{
    public QuickTailorRequestValidator()
    {
        RuleFor(x => x.Input).NotEmpty().WithMessage("Cole o link ou o texto da vaga.")
            .MaximumLength(60000).WithMessage("Entrada muito longa.");
        RuleFor(x => x.JobUrl).MaximumLength(2000).WithMessage("URL muito longa.");
    }
}
