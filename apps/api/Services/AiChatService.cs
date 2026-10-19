using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public interface IAiChatService
{
    Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile);
    Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input);
    Task<JobRequirementExtractionResponse?> ExtractJobRequirementsFromAttachmentsAsync(Guid userId, JobRequirementExtractionInput input, AiInputAttachment[] attachments) =>
        ExtractJobRequirementsAsync(userId, input);
    Task<TailoredResumeGenerationResponse?> GenerateGenericAtsResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
        Task.FromResult<TailoredResumeGenerationResponse?>(null);
    Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input);
}

public partial class AiChatService(AiCompletionService completion) : IAiChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile)
    {
        if (string.IsNullOrWhiteSpace(resumeText) && string.IsNullOrWhiteSpace(publicLinksText)) return null;

        var schema = new
        {
            type = "object",
            properties = new
            {
                fullName = new { type = "string" },
                email = new { type = "string" },
                phone = new { type = "string" },
                city = new { type = "string" },
                state = new { type = "string" },
                country = new { type = "string" },
                linkedinUrl = new { type = "string" },
                githubUrl = new { type = "string" },
                portfolioUrl = new { type = "string" },
                professionalSummary = new { type = "string" },
                skills = new { type = "array", items = new { type = "string" } },
                targetRole = new { type = "string" },
                seniority = new { type = "string" }
            },
            required = new[] { "fullName", "email", "phone", "city", "state", "country", "linkedinUrl", "githubUrl", "portfolioUrl", "professionalSummary", "skills", "targetRole", "seniority" },
            additionalProperties = false
        };

        var prompt = $$"""
            Extraia e estruture o perfil real do candidato a partir do currículo.

            Regras:
            - Use somente fatos presentes no curriculo, links publicos ou perfil existente.
            - Nao invente senioridade, anos, tecnologias ou credenciais.
            - Se uma tecnologia aparece em projetos, portfolio, GitHub ou experiencia, inclua em skills.
            - Inclua TypeScript, JavaScript, Java, Spring Boot, HTML, CSS, React, APIs REST etc quando houver evidencia.
            - Preserve links do curriculo.
            - Retorne apenas JSON valido.

            Perfil existente:
            {{JsonSerializer.Serialize(existingProfile, JsonOptions)}}

            Curriculo extraido:
            {{resumeText[..Math.Min(resumeText.Length, 14000)]}}

            Conteudo publico dos links do curriculo/perfil:
            {{publicLinksText[..Math.Min(publicLinksText.Length, 9000)]}}
            """;

        var raw = await completion.CompleteTextAsync(userId, "resume_profile_import", "You convert resumes and public portfolio text into structured JSON for a job-application profile. You never invent unsupported facts.", prompt, schema);
        var fallback = new ResumeProfileImportData("", "", "", "", "", "", "", "", "", "", [], "", "");
        var parsed = AiChatResponseParser.Parse(raw, fallback);
        return string.IsNullOrWhiteSpace(parsed.ProfessionalSummary) && parsed.Skills.Length == 0
            ? null
            : parsed;
    }

}
