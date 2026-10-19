using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class AddProfileSkillRequestValidator : AbstractValidator<AddProfileSkillRequest>
{
    public AddProfileSkillRequestValidator()
    {
        RuleFor(x => x.Skill).NotEmpty().WithMessage("Informe a habilidade.").MaximumLength(100);
        RuleFor(x => x.Kind).Must(kind => kind is null or "missing" or "related").WithMessage("Tipo de recomendação inválido.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.JobTitle).MaximumLength(200);
        RuleFor(x => x.Company).MaximumLength(200);
    }
}
