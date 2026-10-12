using FluentValidation;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Validators;

public sealed class CreateResumeExportRequestValidator : AbstractValidator<CreateResumeExportRequest>
{
    private static readonly string[] AllowedFormats = ["pdf", "docx", "word", "txt", "text"];

    public CreateResumeExportRequestValidator()
    {
        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("Informe o formato: pdf, docx ou txt.")
            .Must(f => AllowedFormats.Contains((f ?? "").Trim().ToLowerInvariant()))
            .WithMessage("Exportação suporta PDF, DOCX e TXT.");
        RuleFor(x => x.Layout).NotEmpty().MaximumLength(100);
    }
}
