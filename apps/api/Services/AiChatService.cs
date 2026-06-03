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
    Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input);
    Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request);
    Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request);
    Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId);
}

public class AiChatService(HttpClient http, IConfiguration configuration, JobPilotDbContext db, ILogger<AiChatService> logger, AnswerResolver answerResolver, ResumeTextService resumeTextService) : IAiChatService
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
            - CPF cadastrado: {(string.IsNullOrWhiteSpace(profile?.Cpf) ? "nao informado" : "informado no perfil")}
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
        var text = await CompleteTextAsync(userId, "answer_draft", "You are a Brazilian developer answering a job screening question naturally. You hate corporate clichés. You write like you talk.", prompt);
        if (string.IsNullOrWhiteSpace(text))
            return new AnswerDraftResponse("", false, await ActiveModelNameAsync(userId));

        return new AnswerDraftResponse(AnswerHumanizer.Humanize(text), true, await ActiveModelNameAsync(userId));
    }

    public async Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers)
    {
        var decisionPrompt = AgentDecisionHarness.BuildBrowserActionPrompt(request, profile, answers, JsonOptions);
        var raw = await CompleteTextAsync(userId, "browser_action_plan", decisionPrompt.System, decisionPrompt.User, decisionPrompt.Schema);
        var fallback = new BrowserActionPlanResponse("checkpoint", "", "", AgentDecisionHarness.DescribeParseFailure(raw), 0.2m, true);
        var plan = ParseJson(raw, fallback);
        var validation = AgentDecisionHarness.ValidateBrowserActionPlan(plan, request.ObservationJson);
        await RecordDecisionEventAsync(userId, request.ApplicationJobId, request.Attempt, validation.EventType, validation.EventDetail, validation.Plan);
        return validation.Plan;
    }

    public async Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter)
    {
        var decisionPrompt = SearchDecisionHarness.BuildJobFitPrompt(lead, profile, filter, JsonOptions);
        var raw = await CompleteTextAsync(userId, "job_fit_review", decisionPrompt.System, decisionPrompt.User, decisionPrompt.Schema);
        var fallback = SearchDecisionHarness.UnavailableReview(AgentDecisionHarness.DescribeParseFailure(raw));
        var review = ParseJson(raw, fallback);
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
            required = new[] { "fullName", "email", "phone", "city", "country", "linkedinUrl", "githubUrl", "portfolioUrl", "professionalSummary", "skills", "targetRole", "seniority", "answers" },
            additionalProperties = false
        };

        var prompt = $$"""
            Extraia e estruture o perfil real do candidato para um agente de candidaturas.

            Regras:
            - Use somente fatos presentes no curriculo, links publicos ou perfil existente.
            - Nao invente senioridade, anos, tecnologias ou credenciais.
            - Se uma tecnologia aparece em projetos, portfolio, GitHub ou experiencia, inclua em skills.
            - Crie respostas reutilizaveis de triagem para perguntas tecnicas sim/nao e experiencia.
            - Para respostas sim/nao, use "Sim" ou "Não".
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

        var raw = await CompleteTextAsync(userId, "resume_profile_import", "You convert resumes and public portfolio text into structured JSON for a job-application profile. You never invent unsupported facts.", prompt, schema);
        var fallback = new ResumeProfileImportData("", "", "", "", "", "", "", "", "", [], "", "", []);
        var parsed = ParseJson(raw, fallback);
        return string.IsNullOrWhiteSpace(parsed.ProfessionalSummary) && parsed.Skills.Length == 0 && parsed.Answers.Length == 0
            ? null
            : parsed;
    }

    public async Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input)
    {
        if ((input.Attachments ?? []).Length > 0)
            return await ExtractJobRequirementsFromAttachmentsAsync(userId, input, input.Attachments ?? []);

        if (string.IsNullOrWhiteSpace(input.SourceText)) return null;

        var schema = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                company = new { type = "string" },
                location = new { type = "string" },
                seniority = new { type = "string", @enum = new[] { "intern", "junior", "mid", "senior", "lead", "unknown" } },
                workplace = new { type = "string", @enum = new[] { "remote", "hybrid", "onsite", "unknown" } },
                summary = new { type = "string" },
                requirements = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string", @enum = new[] { "technology", "responsibility", "experience", "education", "language", "certification", "domain", "soft_skill", "work_model", "other" } },
                            label = new { type = "string" },
                            importance = new { type = "string", @enum = new[] { "must_have", "nice_to_have", "context" } },
                            evidenceText = new { type = "string" },
                            normalizedKeyword = new { type = "string" },
                            exactPhrases = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "category", "label", "importance", "evidenceText", "normalizedKeyword", "exactPhrases" },
                        additionalProperties = false
                    }
                },
                warnings = new { type = "array", items = new { type = "string" } },
                confidence = new { type = "number", minimum = 0, maximum = 1 }
            },
            required = new[] { "title", "company", "location", "seniority", "workplace", "summary", "requirements", "warnings", "confidence" },
            additionalProperties = false
        };

        var prompt = $$"""
            Organize esta vaga para gerar um curriculo especifico.

            Regras:
            - Extraia apenas informacoes presentes no texto da vaga.
            - Separe requisitos obrigatorios, desejaveis e contexto.
            - Cada requisito deve citar evidenceText com trecho literal ou quase literal da vaga.
            - Nao invente senioridade, tecnologias, empresa, beneficios ou modalidade.
            - Palavras-chave exatas importantes devem aparecer em exactPhrases.
            - Use unknown quando a vaga nao deixar claro.
            - Retorne somente JSON valido.

            Metadados conhecidos:
            {{JsonSerializer.Serialize(input, JsonOptions)}}

            Texto da vaga:
            {{input.SourceText[..Math.Min(input.SourceText.Length, 16000)]}}
            """;

        var raw = await CompleteTextAsync(userId, "job_requirement_extraction", "You structure job descriptions into auditable requirements for resume tailoring. You never invent unsupported facts.", prompt, schema);
        var parsed = ParseJson(raw, new JobRequirementExtractionResponse("", "", "", "unknown", "unknown", "", [], [], 0m));
        return string.IsNullOrWhiteSpace(parsed.Title) && parsed.Requirements.Length == 0
            ? null
            : parsed;
    }

    public async Task<JobRequirementExtractionResponse?> ExtractJobRequirementsFromAttachmentsAsync(Guid userId, JobRequirementExtractionInput input, AiInputAttachment[] attachments)
    {
        if (attachments.Length == 0) return await ExtractJobRequirementsAsync(userId, input);

        var schema = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                company = new { type = "string" },
                location = new { type = "string" },
                seniority = new { type = "string", @enum = new[] { "intern", "junior", "mid", "senior", "lead", "unknown" } },
                workplace = new { type = "string", @enum = new[] { "remote", "hybrid", "onsite", "unknown" } },
                summary = new { type = "string" },
                requirements = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string", @enum = new[] { "technology", "responsibility", "experience", "education", "language", "certification", "domain", "soft_skill", "work_model", "other" } },
                            label = new { type = "string" },
                            importance = new { type = "string", @enum = new[] { "must_have", "nice_to_have", "context" } },
                            evidenceText = new { type = "string" },
                            normalizedKeyword = new { type = "string" },
                            exactPhrases = new { type = "array", items = new { type = "string" } }
                        },
                        required = new[] { "category", "label", "importance", "evidenceText", "normalizedKeyword", "exactPhrases" },
                        additionalProperties = false
                    }
                },
                warnings = new { type = "array", items = new { type = "string" } },
                confidence = new { type = "number", minimum = 0, maximum = 1 }
            },
            required = new[] { "title", "company", "location", "seniority", "workplace", "summary", "requirements", "warnings", "confidence" },
            additionalProperties = false
        };

        var prompt = $$"""
            Leia a imagem ou PDF anexado e organize a vaga para gerar um curriculo especifico.

            Regras:
            - Extraia apenas informacoes visiveis no anexo ou presentes nos metadados.
            - Separe requisitos obrigatorios, desejaveis e contexto.
            - Cada requisito deve citar evidenceText com trecho literal ou quase literal.
            - Nao invente senioridade, tecnologias, empresa, beneficios ou modalidade.
            - Use unknown quando a vaga nao deixar claro.
            - Retorne somente JSON valido.

            Metadados conhecidos:
            {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;

        var raw = await CompleteTextWithProviderAsync(
            await ResolveProviderAsync(userId),
            userId,
            "job_requirement_vision",
            "You structure job screenshots and PDFs into auditable requirements for resume tailoring. You never invent unsupported facts.",
            prompt,
            schema,
            attachments);
        var parsed = ParseJson(raw, new JobRequirementExtractionResponse("", "", "", "unknown", "unknown", "", [], [], 0m));
        return string.IsNullOrWhiteSpace(parsed.Title) && parsed.Requirements.Length == 0
            ? null
            : parsed;
    }

    public async Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.DeterministicDraft)) return null;

        var schema = new
        {
            type = "object",
            properties = new
            {
                plainText = new { type = "string" },
                warnings = new { type = "array", items = new { type = "string" } }
            },
            required = new[] { "plainText", "warnings" },
            additionalProperties = false
        };

        var prompt = $$"""
            Reescreva um curriculo ATS-safe para uma vaga especifica.

            Regras obrigatorias:
            - Retorne texto simples, uma coluna, sem tabelas, sem icones, sem markdown decorativo.
            - Use somente fatos presentes em candidateJson, baseResumeText, deterministicDraft ou evidenceItems.
            - Keywords em includedKeywords podem entrar como competencia direta.
            - Keywords em adjacentKeywords so podem entrar como experiencia relacionada, sem afirmar experiencia direta.
            - Keywords em excludedKeywords NAO podem entrar como competencia, experiencia, projeto ou resumo.
            - Checkpoints devem ficar somente em uma secao final "PENDENCIAS PARA REVISAO".
            - Nao invente anos, empresas, formacao, certificacoes, senioridade, idiomas ou tecnologias.
            - Preserve portugues do Brasil.
            - Retorne apenas JSON valido no schema.

            Entrada:
            {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;

        var raw = await CompleteTextAsync(userId, "resume_tailoring", "You rewrite ATS-safe resumes using only audited evidence. You never invent unsupported candidate facts.", prompt, schema);
        var parsed = ParseJson(raw, new TailoredResumeGenerationResponse("", []));
        return string.IsNullOrWhiteSpace(parsed.PlainText) ? null : parsed;
    }

    public async Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request)
    {
        var system = "Classify a browser page for a job-application automation. Return only JSON.";
        var prompt = $$"""
            URL: {{request.Url}}
            Title: {{request.Title}}
            Visible text: {{request.VisibleText}}

            Return JSON:
            { "state": "login|captcha|job_page|easy_apply_modal|external_application|form|submitted|error|unknown", "reason": "short reason", "confidence": 0.0 }
            """;
        var json = await CompleteTextAsync(userId, "page_state", system, prompt, new
        {
            type = "object",
            properties = new
            {
                state = new { type = "string", @enum = new[] { "login", "captcha", "job_page", "easy_apply_modal", "external_application", "form", "submitted", "error", "unknown" } },
                reason = new { type = "string" },
                confidence = new { type = "number", minimum = 0, maximum = 1 }
            },
            required = new[] { "state", "reason", "confidence" }
            ,
            additionalProperties = false
        });
        return ParseJson(json, LocalPageState(request));
    }

    public async Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request)
    {
        var system = "Validate whether a browser automation step helped a job application. Return only JSON.";
        var prompt = $$"""
            Action JSON: {{request.ActionJson}}
            Job context: {{request.JobContext}}
            Before observation: {{request.BeforeObservationJson}}
            After observation: {{request.AfterObservationJson}}

            Return JSON:
            { "success": true, "reason": "short reason", "confidence": 0.0, "requiresCheckpoint": false }
            """;
        var json = await CompleteTextAsync(userId, "step_validation", system, prompt, new
        {
            type = "object",
            properties = new
            {
                success = new { type = "boolean" },
                reason = new { type = "string" },
                confidence = new { type = "number", minimum = 0, maximum = 1 },
                requiresCheckpoint = new { type = "boolean" }
            },
            required = new[] { "success", "reason", "confidence", "requiresCheckpoint" },
            additionalProperties = false
        });
        return ParseJson(json, new StepValidationResponse(false, AgentDecisionHarness.DescribeParseFailure(json), 0.2m, true));
    }

    public async Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId)
    {
        var provider = await db.AiProviders.FirstOrDefaultAsync(x => x.Id == providerId && x.UserId == userId)
            ?? throw new InvalidOperationException("Provider not found.");
        var text = await CompleteTextWithProviderAsync(provider, userId, "provider_test", "Return a short health-check response.", "Say only: ok");
        return new AnswerDraftResponse(string.IsNullOrWhiteSpace(text) ? "failed" : text, !string.IsNullOrWhiteSpace(text), provider.Model);
    }

    private async Task<string?> CompleteTextAsync(Guid userId, string operation, string system, string prompt, object? format = null)
    {
        var provider = await ResolveProviderAsync(userId);
        return await CompleteTextWithProviderAsync(provider, userId, operation, system, prompt, format);
    }

    private async Task<string?> CompleteTextWithProviderAsync(AiProvider provider, Guid userId, string operation, string system, string prompt, object? format = null, AiInputAttachment[]? attachments = null)
    {
        try
        {
            attachments ??= [];
            string? text;
            if (provider.ApiFormat == "ollama-chat" || provider.ProviderType == "ollama")
            {
                text = await CompleteOllamaChatAsync(provider, system, prompt, format);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return text;
            }

            var apiKey = SecretProtector.Unprotect(provider.EncryptedApiKey, configuration);
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            if (provider.ApiFormat == "anthropic-messages")
            {
                text = await CompleteAnthropicMessagesAsync(provider, apiKey, system, prompt);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return text;
            }
            if (provider.ApiFormat == "openai-responses")
                return await CompleteOpenAiResponsesAsync(provider, userId, operation, apiKey, system, prompt, format, attachments);
            return await CompleteOpenAiCompatibleAsync(provider, userId, operation, apiKey, system, prompt, format);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI completion failed with provider {ProviderName}.", provider.Name);
            return null;
        }
    }

    private async Task<AiProvider> ResolveProviderAsync(Guid userId)
    {
        var provider = await db.AiProviders
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        if (provider is not null) return provider;

        if (configuration.GetValue("LocalLlm:Enabled", true))
        {
            return new AiProvider
            {
                Name = "Ollama Local",
                ProviderType = "ollama",
                BaseUrl = configuration["LocalLlm:BaseUrl"] ?? "http://localhost:11434",
                Model = configuration["LocalLlm:Model"] ?? "qwen3:4b-instruct",
                ApiFormat = "ollama-chat",
                EncryptedApiKey = ""
            };
        }

        return new AiProvider
        {
            Name = "Kimi Code",
            ProviderType = "kimi",
            BaseUrl = configuration["Kimi:BaseUrl"] ?? "https://api.kimi.com/coding",
            Model = configuration["Kimi:Model"] ?? "kimi-for-coding",
            ApiFormat = configuration["Kimi:Api"] ?? "anthropic-messages",
            EncryptedApiKey = SecretProtector.Protect(configuration["Kimi:ApiKey"]
                ?? Environment.GetEnvironmentVariable("KIMI_API_KEY")
                ?? Environment.GetEnvironmentVariable("KIMI_API_KEY", EnvironmentVariableTarget.User), configuration)
        };
    }

    private async Task<string> ActiveModelNameAsync(Guid userId)
    {
        var provider = await ResolveProviderAsync(userId);
        return provider.Model;
    }

    private async Task<string?> CompleteAnthropicMessagesAsync(AiProvider provider, string apiKey, string system, string prompt)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.UserAgent.ParseAdd("jobpilot/0.1.0");
        request.Content = JsonContent.Create(new AnthropicMessagesRequest(provider.Model, system, [new AnthropicMessage("user", prompt)], 900));
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AnthropicMessagesResponse>(JsonOptions);
        return body?.Content.FirstOrDefault(x => x.Type == "text")?.Text?.Trim();
    }

    private async Task<string?> CompleteOpenAiResponsesAsync(AiProvider provider, Guid userId, string operation, string apiKey, string system, string prompt, object? format, AiInputAttachment[] attachments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("OpenAI-Beta", "responses=v1");
        var userContent = BuildOpenAiResponsesUserContent(prompt, attachments);
        var input = new List<OpenAiResponseInputItem>
        {
            new("system", system),
            new("user", userContent)
        };
        var body = format is null
            ? (object)new OpenAiResponsesRequest(provider.Model, input, null)
            : new OpenAiStructuredResponsesRequest(provider.Model, input, new OpenAiTextFormat(new OpenAiTextFormatJsonSchema("jobpilot_structured_response", true, format)));
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenAiResponsesResponse>(JsonOptions);
        await RecordUsageAsync(userId, provider, operation, result?.Usage);
        return result?.Output.FirstOrDefault(x => x.Type == "message")?.Content.FirstOrDefault(x => x.Type == "output_text")?.Text?.Trim();
    }

    private async Task<string?> CompleteOpenAiCompatibleAsync(AiProvider provider, Guid userId, string operation, string apiKey, string system, string prompt, object? format)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var messages = new[] { new ChatMessage("system", system), new ChatMessage("user", prompt) };
        request.Content = format is null
            ? JsonContent.Create(new ChatCompletionRequest(provider.Model, messages, 900))
            : JsonContent.Create(new StructuredChatCompletionRequest(
                provider.Model,
                messages,
                1800,
                new ChatResponseFormat(
                    "json_schema",
                    new ChatJsonSchema("jobpilot_structured_response", true, format))));
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions);
        await RecordUsageAsync(userId, provider, operation, body?.Usage);
        return body?.Choices.FirstOrDefault()?.Message.Content?.Trim();
    }

    private async Task<string?> CompleteOllamaChatAsync(AiProvider provider, string system, string prompt, object? format)
    {
        var noThink = provider.Model.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase) ? " /no_think" : "";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/api/chat");
        request.Content = JsonContent.Create(new OllamaChatRequest(
            provider.Model,
            [new OllamaMessage("system", system + noThink), new OllamaMessage("user", prompt + noThink)],
            false,
            false,
            format,
            new OllamaOptions(0m, 0.9m, 4096, 220)));

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions);
        return StripThinking(body?.Message?.Content);
    }

    private static T ParseJson<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json)) return fallback;
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Replace("```json", "", StringComparison.OrdinalIgnoreCase).Replace("```", "").Trim();
        }
        try
        {
            return JsonSerializer.Deserialize<T>(cleaned, JsonOptions) ?? fallback;
        }
        catch
        {
            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(cleaned[start..(end + 1)], JsonOptions) ?? fallback;
                }
                catch
                {
                    return fallback;
                }
            }

            return fallback;
        }
    }

    private static string? StripThinking(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var cleaned = text.Trim();
        var endThink = cleaned.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (endThink >= 0)
            cleaned = cleaned[(endThink + "</think>".Length)..].Trim();

        return cleaned;
    }

    private async Task RecordDecisionEventAsync(Guid userId, Guid? applicationJobId, int attempt, string? eventType, string? eventDetail, BrowserActionPlanResponse plan)
    {
        if (eventType is null && plan.ActionType != "checkpoint") return;

        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = applicationJobId,
            EventType = eventType ?? "llm.checkpoint",
            Detail = eventDetail ?? $"attempt={attempt}; action={plan.ActionType}; confidence={plan.Confidence:0.00}; reason={plan.Reason}"
        });
        await db.SaveChangesAsync();
    }

    private async Task RecordEstimatedUsageAsync(Guid userId, AiProvider provider, string operation, string system, string prompt, string? output)
    {
        var inputTokens = EstimateTokens(system) + EstimateTokens(prompt);
        var outputTokens = EstimateTokens(output ?? "");
        var totalTokens = inputTokens + outputTokens;
        if (totalTokens <= 0) return;

        var usage = new AiUsage(inputTokens, outputTokens, null, null, totalTokens);
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AiProviderId = provider.Id == Guid.Empty ? null : provider.Id,
            ProviderType = provider.ProviderType,
            ApiFormat = provider.ApiFormat,
            Model = provider.Model,
            Operation = operation,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = EstimateCostUsd(provider.Model, inputTokens, outputTokens),
            UsageJson = JsonSerializer.Serialize(new { estimated = true, inputTokens, outputTokens, totalTokens }, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static int EstimateTokens(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, (int)Math.Ceiling(text.Length / 4m));

    private static object BuildOpenAiResponsesUserContent(string prompt, AiInputAttachment[] attachments)
    {
        if (attachments.Length == 0) return prompt;

        var content = new List<OpenAiResponseContentInput>
        {
            new("input_text", prompt, null, null, null)
        };

        foreach (var attachment in attachments.Where(x => !string.IsNullOrWhiteSpace(x.DataUrl)))
        {
            var normalizedType = attachment.Type.Trim().ToLowerInvariant();
            if (normalizedType == "file" || attachment.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                content.Add(new OpenAiResponseContentInput("file", null, null, attachment.DataUrl, attachment.FileName));
            else
                content.Add(new OpenAiResponseContentInput("image_url", null, attachment.DataUrl, null, attachment.FileName));
        }

        return content;
    }

    private async Task RecordUsageAsync(Guid userId, AiProvider provider, string operation, AiUsage? usage)
    {
        if (usage is null) return;

        var inputTokens = usage.InputTokens ?? usage.PromptTokens ?? 0;
        var outputTokens = usage.OutputTokens ?? usage.CompletionTokens ?? 0;
        var totalTokens = usage.TotalTokens == 0 ? inputTokens + outputTokens : usage.TotalTokens;
        if (totalTokens <= 0) return;

        var cost = EstimateCostUsd(provider.Model, inputTokens, outputTokens);
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AiProviderId = provider.Id == Guid.Empty ? null : provider.Id,
            ProviderType = provider.ProviderType,
            ApiFormat = provider.ApiFormat,
            Model = provider.Model,
            Operation = operation,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = cost,
            UsageJson = JsonSerializer.Serialize(usage, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private decimal EstimateCostUsd(string model, int inputTokens, int outputTokens)
    {
        var normalized = model.ToLowerInvariant();
        var inputPerMillion = configuration.GetValue<decimal?>($"AiCosts:{model}:InputPerMillionUsd");
        var outputPerMillion = configuration.GetValue<decimal?>($"AiCosts:{model}:OutputPerMillionUsd");

        if (inputPerMillion is null || outputPerMillion is null)
        {
            (inputPerMillion, outputPerMillion) = normalized switch
            {
                var value when value.Contains("gpt-4.1-mini") => (0.40m, 1.60m),
                var value when value.Contains("gpt-4.1") => (2.00m, 8.00m),
                var value when value.Contains("gpt-4o-mini") => (0.15m, 0.60m),
                var value when value.Contains("gpt-4o") => (2.50m, 10.00m),
                _ => (0m, 0m)
            };
        }

        return Math.Round((inputTokens / 1_000_000m * inputPerMillion.Value) + (outputTokens / 1_000_000m * outputPerMillion.Value), 6);
    }

    private static PageStateResponse LocalPageState(PageStateRequest request)
    {
        var text = request.VisibleText.ToLowerInvariant();
        var url = request.Url.ToLowerInvariant();
        if (url.Contains("checkpoint/challenge") || text.Contains("captcha") || text.Contains("verify your identity")) return new("captcha", "Security challenge detected.", 0.9m);
        if (url.Contains("/login") || (text.Contains("email") && text.Contains("password"))) return new("login", "Login form detected.", 0.8m);
        if (text.Contains("candidatura simplificada") || text.Contains("easy apply")) return new("job_page", "LinkedIn application controls are visible.", 0.7m);
        if (text.Contains("submit") || text.Contains("enviar") || text.Contains("avançar") || text.Contains("next")) return new("form", "Application form controls are visible.", 0.6m);
        return new("unknown", "No clear application state detected.", 0.4m);
    }

    private sealed record ChatCompletionRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] ChatMessage[] Messages, [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens);
    private sealed record StructuredChatCompletionRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] ChatMessage[] Messages, [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens, [property: JsonPropertyName("response_format")] ChatResponseFormat ResponseFormat);
    private sealed record ChatResponseFormat([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("json_schema")] ChatJsonSchema JsonSchema);
    private sealed record ChatJsonSchema([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("strict")] bool Strict, [property: JsonPropertyName("schema")] object Schema);
    private sealed record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] List<ChatChoice> Choices, [property: JsonPropertyName("usage")] AiUsage? Usage);
    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);
    private sealed record AnthropicMessagesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("system")] string System, [property: JsonPropertyName("messages")] AnthropicMessage[] Messages, [property: JsonPropertyName("max_tokens")] int MaxTokens);
    private sealed record AnthropicMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record AnthropicMessagesResponse([property: JsonPropertyName("content")] List<AnthropicContent> Content);
    private sealed record AnthropicContent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);
    private sealed record OllamaChatRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] OllamaMessage[] Messages, [property: JsonPropertyName("stream")] bool Stream, [property: JsonPropertyName("think")] bool Think, [property: JsonPropertyName("format")] object? Format, [property: JsonPropertyName("options")] OllamaOptions Options);
    private sealed record OllamaMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record OllamaOptions([property: JsonPropertyName("temperature")] decimal Temperature, [property: JsonPropertyName("top_p")] decimal TopP, [property: JsonPropertyName("num_ctx")] int NumCtx, [property: JsonPropertyName("num_predict")] int NumPredict);
    private sealed record OllamaChatResponse([property: JsonPropertyName("message")] OllamaMessage? Message);

    private sealed record OpenAiResponsesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("input")] List<OpenAiResponseInputItem> Input, [property: JsonPropertyName("text")] OpenAiTextFormat? Text);
    private sealed record OpenAiStructuredResponsesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("input")] List<OpenAiResponseInputItem> Input, [property: JsonPropertyName("text")] OpenAiTextFormat Text);
    private sealed record OpenAiResponseInputItem([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] object Content);
    private sealed record OpenAiResponseContentInput([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string? Text, [property: JsonPropertyName("image_url")] string? ImageUrl, [property: JsonPropertyName("file")] string? File, [property: JsonPropertyName("filename")] string? FileName);
    private sealed record OpenAiTextFormat([property: JsonPropertyName("format")] OpenAiTextFormatJsonSchema Format);
    private sealed record OpenAiTextFormatJsonSchema([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("strict")] bool Strict, [property: JsonPropertyName("schema")] object Schema);
    private sealed record OpenAiResponsesResponse([property: JsonPropertyName("output")] List<OpenAiResponseOutput> Output, [property: JsonPropertyName("usage")] AiUsage? Usage);
    private sealed record OpenAiResponseOutput([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("content")] List<OpenAiResponseContent> Content);
    private sealed record OpenAiResponseContent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);
    private sealed record AiUsage(
        [property: JsonPropertyName("input_tokens")] int? InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens,
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}

public record AnswerDraftResponse(string Text, bool UsedKimi, string Model);
