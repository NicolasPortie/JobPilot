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
        RuleFor(x => x.Url).Must(QuickTailorRequestValidator.IsHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("O link da vaga precisa começar com http:// ou https://.");
        When(x => (x.SourceKind ?? "").Trim().ToLowerInvariant() == "url", () =>
        {
            RuleFor(x => x.Url).NotEmpty().WithMessage("Informe a URL da vaga.");
        });
    }
}
