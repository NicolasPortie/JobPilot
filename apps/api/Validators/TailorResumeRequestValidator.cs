using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class TailorResumeRequestValidator : AbstractValidator<TailorResumeRequest>
{
    public TailorResumeRequestValidator()
    {
        RuleFor(x => x.BaseResumeVersionId).NotEmpty().WithMessage("Informe a versão base do currículo.");
        RuleFor(x => x.JobTargetId).NotEmpty().WithMessage("Informe a vaga para adaptar.");
        RuleFor(x => x.Layout).NotEmpty().MaximumLength(100);
    }
}
