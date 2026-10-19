using JobPilot.Api.Domain;

namespace JobPilot.Api.Application.Contracts;

public record ProfileImportResponse(
    CandidateProfile Profile,
    string[] SkillsAdded,
    string[] LinksFetched,
    string[] Warnings,
    ResumeExtractionReport Extraction,
    string ExtractedText,
    ResumeProfileImportData ImportData);

/// <summary>
/// Resposta segura para o upload iniciado pela aplicação web. O perfil não
/// expõe CPF nem ponteiros físicos de armazenamento.
/// </summary>
public sealed record ResumeFileImportResponse(
    CandidateProfileResponse Profile,
    string[] SkillsAdded,
    string[] LinksFetched,
    string[] Warnings,
    ResumeExtractionReport Extraction,
    string ExtractedText,
    ResumeProfileImportData ImportData)
{
    public static ResumeFileImportResponse From(ProfileImportResponse response) =>
        new(
            CandidateProfileResponse.From(response.Profile),
            response.SkillsAdded,
            response.LinksFetched,
            response.Warnings,
            response.Extraction,
            response.ExtractedText,
            response.ImportData);
}
