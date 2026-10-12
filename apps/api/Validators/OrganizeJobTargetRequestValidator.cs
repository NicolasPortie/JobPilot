using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class OrganizeJobTargetRequestValidator : AbstractValidator<OrganizeJobTargetRequest>
{
    public OrganizeJobTargetRequestValidator()
    {
        RuleFor(x => x.SourceKind).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Text).MaximumLength(20000).WithMessage("Texto da vaga muito longo (máx. 20000 caracteres).");
        RuleFor(x => x.Url).MaximumLength(2000).WithMessage("URL muito longa.");
        When(x => (x.SourceKind ?? "").Trim().ToLowerInvariant() == "url", () =>
        {
            RuleFor(x => x.Url).NotEmpty().WithMessage("Informe a URL da vaga.");
        });
    }
}
