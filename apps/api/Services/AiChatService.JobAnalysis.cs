using System.Text.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public partial class AiChatService
{
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
            Analise esta descricao de vaga e extraia requisitos estruturados para gerar um curriculo direcionado.

            INSTRUCOES DE EXTRACAO:
            1. Leia o texto completo e identifique as secoes: Requisitos obrigatorios, Desejavel/Diferencial, Beneficios, Responsabilidades, Sobre a vaga.
            2. Extraia CADA tecnologia, ferramenta, habilidade ou responsabilidade como um requisito separado.
            3. O campo "label" deve ser CURTO e especifico (ex: "C#", ".NET 8", "Docker", "PostgreSQL", "Clean Architecture"). Nao agrupe varias tecnologias em um unico label.
            4. "importance":
               - "must_have" = obrigatorio, requisitos, necessario, experiencia com, exigido
               - "nice_to_have" = desejavel, diferencial, seria um plus, conhecimento em, sera considerado diferencial
               - "context" = responsabilidade da vaga, beneficio, formato de trabalho, sobre a empresa
            5. "evidenceText" deve ser APENAS a frase ou item de lista onde o requisito aparece. Maximo 80 caracteres. Nunca inclua paragrafos inteiros ou a lista completa de requisitos.
            6. "exactPhrases" deve conter a forma exata como a tecnologia aparece na vaga (ex: [".NET 8"], ["Entity Framework Core"]).
            7. "title": A PRIMEIRA LINHA do texto da vaga geralmente contem o cargo/titulo (ex: "Desenvolvedor Back-end .NET PL"). Extraia-o diretamente. Nunca deixe em branco.
            8. Nao invente tecnologias, senioridade, empresa ou modalidade que nao aparecam no texto.
            9. Retorne SOMENTE JSON valido.

            EXEMPLO DE ENTRADA:
            Desenvolvedor Python Senior
            Requisitos:
            - Python 3.11, Django, FastAPI
            - PostgreSQL, Redis
            - Docker, AWS
            Desejavel:
            - Experiencia com GraphQL
            - Ingles avancado

            EXEMPLO DE SAIDA:
            {
              "title": "Desenvolvedor Python Senior",
              "company": "",
              "location": "",
              "seniority": "senior",
              "workplace": "unknown",
              "summary": "Vaga para desenvolvedor Python Senior com Django, FastAPI, PostgreSQL, Redis, Docker e AWS.",
              "requirements": [
                {"category": "technology", "label": "Python", "importance": "must_have", "evidenceText": "- Python 3.11, Django, FastAPI", "normalizedKeyword": "python", "exactPhrases": ["Python 3.11"]},
                {"category": "technology", "label": "Django", "importance": "must_have", "evidenceText": "- Python 3.11, Django, FastAPI", "normalizedKeyword": "django", "exactPhrases": ["Django"]},
                {"category": "technology", "label": "FastAPI", "importance": "must_have", "evidenceText": "- Python 3.11, Django, FastAPI", "normalizedKeyword": "fastapi", "exactPhrases": ["FastAPI"]},
                {"category": "technology", "label": "PostgreSQL", "importance": "must_have", "evidenceText": "- PostgreSQL, Redis", "normalizedKeyword": "postgresql", "exactPhrases": ["PostgreSQL"]},
                {"category": "technology", "label": "Redis", "importance": "must_have", "evidenceText": "- PostgreSQL, Redis", "normalizedKeyword": "redis", "exactPhrases": ["Redis"]},
                {"category": "technology", "label": "Docker", "importance": "must_have", "evidenceText": "- Docker, AWS", "normalizedKeyword": "docker", "exactPhrases": ["Docker"]},
                {"category": "technology", "label": "AWS", "importance": "must_have", "evidenceText": "- Docker, AWS", "normalizedKeyword": "aws", "exactPhrases": ["AWS"]},
                {"category": "technology", "label": "GraphQL", "importance": "nice_to_have", "evidenceText": "- Experiencia com GraphQL", "normalizedKeyword": "graphql", "exactPhrases": ["GraphQL"]},
                {"category": "language", "label": "Ingles", "importance": "nice_to_have", "evidenceText": "- Ingles avancado", "normalizedKeyword": "ingles", "exactPhrases": ["Ingles avancado"]}
              ],
              "warnings": [],
              "confidence": 0.95
            }

            CALCULO DE CONFIANCA (campo "confidence"):
            - 0.95-1.00: Vaga perfeitamente estruturada: titulo claro na primeira linha, secoes de Requisitos/Desejavel/Beneficios, 8+ requisitos, evidencias fortes
            - 0.90-0.94: Vaga bem estruturada com secoes claras, requisitos em lista, texto completo
            - 0.75-0.89: Requisitos extraidos mas titulo precisou ser inferido ou texto parcial
            - 0.40-0.74: Poucos requisitos, texto curto, ou ambiguidades significativas
            - 0.00-0.39: Texto insuficiente ou vaga confusa

            REGRAS OBRIGATORIAS PARA CONFIDENCE:
            1. Se o texto tem secoes claras (Requisitos, Desejavel, Beneficios) E 300+ caracteres E 5+ requisitos extraidos: confidence DEVE ser >= 0.92.
            2. Se o texto tem titulo na primeira linha E secoes bem definidas E 8+ requisitos: confidence DEVE ser >= 0.95.
            3. NUNCA subestime a confianca de textos estruturados. Se as secoes sao legiveis e os requisitos sao claros, use 0.92 ou mais.
            4. A confianca mede a QUALIDADE da extracao, nao a completude do perfil do candidato.

            Metadados conhecidos:
            {{JsonSerializer.Serialize(input, JsonOptions)}}

            Texto da vaga:
            {{input.SourceText[..Math.Min(input.SourceText.Length, 16000)]}}
            """;

        var raw = await completion.CompleteTextAsync(userId, "job_requirement_extraction", "You structure job descriptions into auditable requirements for resume tailoring. You never invent unsupported facts.", prompt, schema);
        var parsed = AiChatResponseParser.Parse(raw, new JobRequirementExtractionResponse("", "", "", "unknown", "unknown", "", [], [], 0m));
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

        var raw = await completion.CompleteTextWithProviderAsync(
            await completion.ResolveProviderAsync(userId),
            userId,
            "job_requirement_vision",
            "You structure job screenshots and PDFs into auditable requirements for resume tailoring. You never invent unsupported facts.",
            prompt,
            schema,
            attachments);
        var parsed = AiChatResponseParser.Parse(raw, new JobRequirementExtractionResponse("", "", "", "unknown", "unknown", "", [], [], 0m));
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
            - Nao inclua secoes de auditoria interna como evidencias, pontos relacionados, historico base, checkpoints ou pendencias no curriculo final.
            - Use secoes normais de curriculo: RESUMO PROFISSIONAL, COMPETENCIAS, EXPERIENCIA PROFISSIONAL, PROJETOS, FORMACAO, CERTIFICACOES, IDIOMAS.
            - A vaga deve influenciar ordem, enfase e palavras-chave, sem parecer relatorio sobre a vaga.
            - Nao inclua o nome da empresa da vaga no titulo profissional, resumo, competencias ou qualquer outra linha do curriculo. O curriculo e direcionado para a vaga, mas nao deve dizer "Cargo | Empresa".
            - Nao invente anos, empresas, formacao, certificacoes, senioridade, idiomas ou tecnologias.
            - Preserve portugues do Brasil.
            - Mantenha literalmente marcadores como [redacted-email-1] e [redacted-phone-2] na linha de contato; eles sao substituidos pelos dados reais depois.
            - Retorne apenas JSON valido no schema.

            Entrada:
            {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;

        var raw = await completion.CompleteTextAsync(userId, "resume_tailoring", "You rewrite ATS-safe resumes using only audited evidence. You never invent unsupported candidate facts.", prompt, schema);
        var parsed = AiChatResponseParser.Parse(raw, new TailoredResumeGenerationResponse("", []));
        return string.IsNullOrWhiteSpace(parsed.PlainText) ? null : parsed;
    }

    public async Task<TailoredResumeGenerationResponse?> GenerateGenericAtsResumeAsync(Guid userId, TailoredResumeGenerationInput input)
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
            Reescreva um curriculo ATS-safe geral, rico e verdadeiro.

            Objetivo:
            - Melhorar clareza, densidade e impacto profissional sem perder compatibilidade ATS.
            - Transformar descricoes fracas em bullets mais fortes quando o fato ja estiver no texto base.
            - Preservar tecnologias, empresas, cargos, datas, links, formacao e projetos encontrados.

            Regras obrigatorias:
            - Retorne texto simples, uma coluna, sem tabelas, sem icones, sem markdown decorativo.
            - Use apenas fatos presentes em candidateJson, baseResumeText ou deterministicDraft.
            - Nao invente metricas, anos, empresas, formacao, certificacoes, senioridade, idiomas, tecnologias ou resultados.
            - Nao use reticencias para cortar conteudo. Se precisar reduzir, remova a frase menos importante.
            - Nao crie secao OBJETIVO se ela apenas repetir o cargo. Use o cargo alvo na linha de titulo profissional abaixo do nome.
            - Use secoes padrao: RESUMO PROFISSIONAL, COMPETENCIAS, EXPERIENCIA PROFISSIONAL, PROJETOS, FORMACAO, CERTIFICACOES, IDIOMAS.
            - RESUMO PROFISSIONAL deve ter 2 a 3 frases fortes, com identidade profissional, stack principal, tipo de experiencia e contexto de projetos, sempre baseado em fatos do texto base.
            - Em EXPERIENCIA PROFISSIONAL e PROJETOS, prefira bullets com acao, tecnologia/contexto e resultado operacional quando houver evidencia no texto.
            - Para layout ats_compact, seja conciso. Para ats_standard, preserve mais evidencias e aceite ate duas paginas.
            - Preserve portugues do Brasil.
            - Mantenha literalmente marcadores como [redacted-email-1] e [redacted-phone-2] na linha de contato; eles sao substituidos pelos dados reais depois.
            - Retorne apenas JSON valido no schema.

            Entrada:
            {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;

        var raw = await completion.CompleteTextAsync(userId, "generic_ats_resume", "You rewrite ATS-safe resumes using only candidate evidence. You never invent unsupported facts.", prompt, schema);
        var parsed = AiChatResponseParser.Parse(raw, new TailoredResumeGenerationResponse("", []));
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
        var json = await completion.CompleteTextAsync(userId, "page_state", system, prompt, new
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
        return AiChatResponseParser.Parse(json, AiChatResponseParser.LocalPageState(request));
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
        var json = await completion.CompleteTextAsync(userId, "step_validation", system, prompt, new
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
        return AiChatResponseParser.Parse(json, new StepValidationResponse(false, AgentDecisionHarness.DescribeParseFailure(json), 0.2m, true));
    }

    public async Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId)
    {
        var provider = await db.AiProviders.FirstOrDefaultAsync(x => x.Id == providerId && x.UserId == userId)
            ?? throw new InvalidOperationException("Provider not found.");
        var text = await completion.CompleteTextWithProviderAsync(provider, userId, "provider_test", "Return a short health-check response.", "Say only: ok");
        return new AnswerDraftResponse(string.IsNullOrWhiteSpace(text) ? "failed" : text, !string.IsNullOrWhiteSpace(text), provider.Model);
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
}
