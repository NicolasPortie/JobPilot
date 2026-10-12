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
    Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile);
    Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers);
    Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter);
    Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile);
    Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input);
    Task<JobRequirementExtractionResponse?> ExtractJobRequirementsFromAttachmentsAsync(Guid userId, JobRequirementExtractionInput input, AiInputAttachment[] attachments) =>
        ExtractJobRequirementsAsync(userId, input);
    Task<TailoredResumeGenerationResponse?> GenerateGenericAtsResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
        Task.FromResult<TailoredResumeGenerationResponse?>(null);
    Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input);
    Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request);
    Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request);
    Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId);
}

public partial class AiChatService(JobPilotDbContext db, AnswerResolver answerResolver, ResumeTextService resumeTextService, AiCompletionService completion) : IAiChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile)
    {
        var resolved = await answerResolver.ResolveAsync(userId, questionText, jobContext ?? "", profile);
        if (resolved.Confidence >= 0.75m && resolved.Source is "profile_template" or "reusable_template")
        {
            var humanized = AnswerHumanizer.Humanize(resolved.AnswerValue);
            return new AnswerDraftResponse(humanized, true, "pattern");
        }

        var resumeText = resumeTextService.ReadResumeText(profile?.ResumePath);
        var prompt = $"""
            Voce e o candidato. Responda como se estivesse conversando com um recrutador no WhatsApp.

            REGRAS RIGIDAS:
            - NAO use frases como "Sou um profissional dedicado", "Tenho paixao por tecnologia", "Estou em busca de novos desafios", "sou proativo", "aprendo rapido"
            - NAO use listas numeradas ou bullet points
            - NAO use adjetivos genericos (excelente, incrivel, excepcional, extraordinario)
            - Use a MESMA informalidade/formalidade do tom da pergunta
            - Mencione detalhes REAIS do perfil abaixo. NAO invente nada
            - Use o texto do curriculo abaixo como fonte principal quando o perfil estruturado estiver incompleto
            - Responda em 1-2 frases curtas, no tom de quem realmente quer a vaga mas nao esta desesperado
            - Se a pergunta pedir apenas numero, responda somente o numero

            Perfil real:
            - Nome: {profile?.FullName}
            - Cargo alvo: {profile?.TargetRole}
            - Skills: {string.Join(", ", profile?.Skills ?? [])}
            - Cidade: {profile?.City}
            - Resumo: {profile?.ProfessionalSummary}
            - Experiencia: {profile?.ProfessionalSummary}
            - Curriculo extraido: {resumeText}

            Pergunta: {questionText}
            Contexto da vaga: {jobContext}

            Responda em portugues do Brasil.
            """;
        var text = await completion.CompleteTextAsync(userId, "answer_draft", "You are a Brazilian developer answering a job screening question naturally. You hate corporate cliches. You write like you talk.", prompt);
        if (string.IsNullOrWhiteSpace(text))
            return new AnswerDraftResponse("", false, await completion.ActiveModelNameAsync(userId));

        return new AnswerDraftResponse(AnswerHumanizer.Humanize(text), true, await completion.ActiveModelNameAsync(userId));
    }

    public async Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers)
    {
        var decisionPrompt = AgentDecisionHarness.BuildBrowserActionPrompt(request, profile, answers, JsonOptions);
        var raw = await completion.CompleteTextAsync(userId, "browser_action_plan", decisionPrompt.System, decisionPrompt.User, decisionPrompt.Schema);
        var fallback = new BrowserActionPlanResponse("checkpoint", "", "", AgentDecisionHarness.DescribeParseFailure(raw), 0.2m, true);
        var plan = AiChatResponseParser.Parse(raw, fallback);
        var validation = AgentDecisionHarness.ValidateBrowserActionPlan(plan, request.ObservationJson);
        await RecordDecisionEventAsync(userId, request.ApplicationJobId, request.Attempt, validation.EventType, validation.EventDetail, validation.Plan);
        return validation.Plan;
    }

    public async Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter)
    {
        var decisionPrompt = SearchDecisionHarness.BuildJobFitPrompt(lead, profile, filter, JsonOptions);
        var raw = await completion.CompleteTextAsync(userId, "job_fit_review", decisionPrompt.System, decisionPrompt.User, decisionPrompt.Schema);
        var fallback = SearchDecisionHarness.UnavailableReview(AgentDecisionHarness.DescribeParseFailure(raw));
        var review = AiChatResponseParser.Parse(raw, fallback);
        return SearchDecisionHarness.Validate(review, lead, filter);
    }

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
                seniority = new { type = "string" },
                answers = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            questionKey = new { type = "string" },
                            questionText = new { type = "string" },
                            answerValue = new { type = "string" },
                            category = new { type = "string" }
                        },
                        required = new[] { "questionKey", "questionText", "answerValue", "category" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "fullName", "email", "phone", "city", "state", "country", "linkedinUrl", "githubUrl", "portfolioUrl", "professionalSummary", "skills", "targetRole", "seniority", "answers" },
            additionalProperties = false
        };

        var prompt = $$"""
            Extraia e estruture o perfil real do candidato para um agente de candidaturas.

            Regras:
            - Use somente fatos presentes no curriculo, links publicos ou perfil existente.
            - Nao invente senioridade, anos, tecnologias ou credenciais.
            - Se uma tecnologia aparece em projetos, portfolio, GitHub ou experiencia, inclua em skills.
            - Crie respostas reutilizaveis de triagem para perguntas tecnicas sim/nao e experiencia.
            - Para respostas sim/nao, use "Sim" ou "Nao".
            - Para tecnologia fora do perfil, nao crie resposta negativa generica; crie negativa apenas quando o texto indicar outra stack claramente.
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
        var fallback = new ResumeProfileImportData("", "", "", "", "", "", "", "", "", "", [], "", "", []);
        var parsed = AiChatResponseParser.Parse(raw, fallback);
        return string.IsNullOrWhiteSpace(parsed.ProfessionalSummary) && parsed.Skills.Length == 0 && parsed.Answers.Length == 0
            ? null
            : parsed;
    }

}
public record AnswerDraftResponse(string Text, bool UsedKimi, string Model);
