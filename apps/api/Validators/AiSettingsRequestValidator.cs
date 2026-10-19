using FluentValidation;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Services;

namespace JobPilot.Api.Validators;

public sealed class AiSettingsRequestValidator : AbstractValidator<AiSettingsRequest>
{
    public AiSettingsRequestValidator()
    {
        RuleFor(x => x.ProviderType).Must(AiSettingsService.IsKnownType).WithMessage("Escolha um provedor de IA da lista.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Dê um nome para esta IA.").MaximumLength(80);
        RuleFor(x => x.BaseUrl).NotEmpty().WithMessage("Informe o endereço do serviço.").MaximumLength(300);
        RuleFor(x => x.Model).NotEmpty().WithMessage("Informe o modelo.").MaximumLength(120);
        RuleFor(x => x.ApiKey).MaximumLength(500).WithMessage("Chave muito longa.");
    }
}
