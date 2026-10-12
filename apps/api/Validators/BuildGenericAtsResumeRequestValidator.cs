using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class BuildGenericAtsResumeRequestValidator : AbstractValidator<BuildGenericAtsResumeRequest>
{
    public BuildGenericAtsResumeRequestValidator()
    {
        RuleFor(x => x.BaseResumeVersionId).NotEmpty().WithMessage("Informe a versão base do currículo.");
        RuleFor(x => x.Layout).NotEmpty().MaximumLength(100);
    }
}
