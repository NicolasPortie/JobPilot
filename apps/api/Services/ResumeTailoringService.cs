using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeTailoringService(JobPilotDbContext db, ResumeEvidenceMapService evidenceMapService, IAiChatService ai) : IResumeTailoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static readonly ResumeLayoutOption[] LayoutOptions =
    [
        new("ats_standard", "ATS padrao", "Padrao recomendado: uma coluna ATS-safe, com contexto suficiente para experiencia, projetos e palavras-chave reais.", "Uma coluna, texto selecionavel, sem tabelas, caixas, colunas, icones, imagens, headers ou footers.", true),
        new("ats_compact", "ATS compacto", "Use quando precisa tentar caber em uma pagina sem perder parsing ATS.", "Uma coluna, texto selecionavel e mesmas regras ATS, com cortes mais agressivos de conteudo.", true),
        new("human_review", "Revisao humana", "Versao para envio direto a uma pessoa, quando aparencia e leitura humana importam mais que parser ATS.", "Continua em uma coluna e texto selecionavel, mas prioriza leitura confortavel.", false)
    ];

    public async Task<TailoredResumeResponse> TailorAsync(Guid userId, TailorResumeRequest request)
    {
        var useTransaction = db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction ? await db.Database.BeginTransactionAsync() : null;
        var layout = ResumeTextSanitizer.NormalizeLayout(request.Layout);
        var baseVersion = await db.ResumeVersions
            .Include(x => x.Document)
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.BaseResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão base de currículo não encontrada.");

        var target = await db.JobTargets
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == request.JobTargetId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga alvo não encontrada.");

        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var evidenceMap = request.EvidenceMapRunId is { } mapId
            ? await evidenceMapService.GetAsync(userId, mapId)
            : await evidenceMapService.BuildAsync(userId, new BuildResumeEvidenceMapRequest(baseVersion.Id, target.Id));
        if (evidenceMap is null || evidenceMap.ResumeVersionId != baseVersion.Id || evidenceMap.JobTargetId != target.Id)
            throw new InvalidOperationException("Mapa de evidência não encontrado para esta versão e vaga.");

        var analysis = ResumeTailoringAnalyzer.AnalyzeEvidence(evidenceMap);
        var warnings = ResumeTailoringAnalyzer.BuildWarnings(evidenceMap, analysis);
        var tailoredModel = ResumeTailoringAnalyzer.BuildTailoredModel(profile, baseVersion, target, analysis);
        var deterministicDraft = ResumeComposer.Render(tailoredModel, layout);
        var aiResult = request.UseAi
            ? await TryGenerateWithAiAsync(userId, profile, baseVersion, target, evidenceMap, analysis, deterministicDraft, layout)
            : null;
        var plainText = ResumeTailoringAnalyzer.SanitizeFinalPlainText(aiResult?.PlainText ?? deterministicDraft, target);
        // Com texto de IA, as seções estruturadas acompanham o que a IA escreveu.
        if (aiResult is not null) tailoredModel = ResumeTailoringAnalyzer.ModelFromText(plainText, tailoredModel);
        var status = analysis.Checkpoints.Length > 0 || warnings.Length > 0 ? "needs_review" : "ready";
        var scoreReport = ResumeTailoringAnalyzer.BuildScoreReport(evidenceMap, analysis);
        var score = scoreReport.Score;
        var reviewQuestions = ResumeTailoringAnalyzer.BuildReviewQuestions(evidenceMap);
        if (aiResult is not null)
            warnings = warnings.Concat(aiResult.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var now = DateTimeOffset.UtcNow;

        var tailored = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = baseVersion.ResumeDocumentId,
            JobLeadId = target.JobLeadId,
            Title = ResumeTailoringAnalyzer.BuildTitle(target),
            TargetCompany = target.Company,
            TargetRole = ResumeTailoringAnalyzer.CleanTargetRole(target),
            Status = status,
            AtsScore = score,
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };
        tailored.Sections.AddRange(ResumeTailoringAnalyzer.BuildSections(userId, tailored.Id, profile, target, analysis, tailoredModel, plainText));
        var sectionDiffs = ResumeTailoringAnalyzer.BuildSectionDiffs(baseVersion, tailored);
        tailored.AtsReportJson = JsonSerializer.Serialize(new
        {
            kind = "tailored_resume",
            layout,
            generation = aiResult is null ? "backend-deterministic" : "ai-structured",
            baseVersionId = baseVersion.Id,
            jobTargetId = target.Id,
            evidenceMapRunId = evidenceMap.Id,
            evidenceScore = evidenceMap.EvidenceScore,
            evidenceConfidence = evidenceMap.Confidence,
            includedKeywords = analysis.IncludedKeywords,
            adjacentKeywords = analysis.AdjacentKeywords,
            excludedKeywords = analysis.ExcludedKeywords,
            checkpoints = analysis.Checkpoints,
            scoreComponents = scoreReport.Components,
            sectionDiffs,
            reviewQuestions,
            warnings
        }, JsonOptions);

        baseVersion.Document!.UpdatedAt = now;
        db.ResumeVersions.Add(tailored);
        db.ResumeTailoringRuns.Add(new ResumeTailoringRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = tailored.Id,
            JobLeadId = target.JobLeadId,
            ProviderModel = aiResult is null ? "backend-deterministic" : "ai-structured",
            PromptVersion = "resume-tailoring-v1",
            ResultJson = JsonSerializer.Serialize(new
            {
                baseVersionId = baseVersion.Id,
                jobTargetId = target.Id,
                evidenceMapRunId = evidenceMap.Id,
                tailoredVersionId = tailored.Id,
                status,
                score,
                scoreComponents = scoreReport.Components,
                sectionDiffs,
                reviewQuestions,
                layout,
                generation = aiResult is null ? "backend-deterministic" : "ai-structured"
            }, JsonOptions),
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        return new TailoredResumeResponse(
            tailored.Id,
            baseVersion.Id,
            target.Id,
            evidenceMap.Id,
            status,
            score,
            scoreReport.Components,
            sectionDiffs,
            reviewQuestions,
            plainText,
            analysis.IncludedKeywords,
            analysis.AdjacentKeywords,
            analysis.ExcludedKeywords,
            analysis.Checkpoints,
            warnings,
            ResumeTailoringAnalyzer.ToVersionDetail(tailored),
            aiResult is not null);
    }

    public async Task<TailoredResumeResponse> BuildGenericAtsAsync(Guid userId, BuildGenericAtsResumeRequest request)
    {
        var useTransaction = db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        await using var transaction = useTransaction ? await db.Database.BeginTransactionAsync() : null;
        var layout = ResumeTextSanitizer.NormalizeLayout(request.Layout);
        var baseVersion = await db.ResumeVersions
            .Include(x => x.Document)
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.BaseResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão base de currículo não encontrada.");
        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);

        var deterministicDraft = ResumeDraftBuilder.BuildGenericAtsPlainText(profile, baseVersion, layout);
        var aiResult = request.UseAi
            ? await TryGenerateGenericAtsWithAiAsync(userId, profile, baseVersion, deterministicDraft, layout)
            : null;
        var plainText = ResumeTailoringAnalyzer.SanitizeFinalPlainText(aiResult?.PlainText ?? deterministicDraft);
        if (string.IsNullOrWhiteSpace(plainText) || plainText.Length < 120)
            throw new InvalidOperationException("Currículo base sem conteúdo suficiente para gerar uma versão ATS.");

        var now = DateTimeOffset.UtcNow;
        var scoreReport = ResumeDraftBuilder.BuildGenericScoreReport(plainText, baseVersion.Sections);
        var atsVersion = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = baseVersion.ResumeDocumentId,
            Title = $"Curriculo ATS - {ResumeTextSanitizer.LayoutLabel(layout)}",
            TargetCompany = "",
            TargetRole = ResumeTextSanitizer.FirstNonEmpty(baseVersion.TargetRole, profile?.TargetRole),
            Status = scoreReport.Score >= 70 ? "ready" : "needs_review",
            AtsScore = scoreReport.Score,
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };
        atsVersion.Sections.AddRange(ResumeDraftBuilder.BuildGenericSections(userId, atsVersion.Id, profile, baseVersion, plainText));
        var sectionDiffs = ResumeTailoringAnalyzer.BuildSectionDiffs(baseVersion, atsVersion);
        var warnings = ResumeDraftBuilder.BuildGenericWarnings(plainText, baseVersion.Sections);
        if (aiResult is not null)
            warnings = warnings.Concat(aiResult.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var generation = aiResult is null ? "backend-deterministic" : "ai-structured";
        atsVersion.AtsReportJson = JsonSerializer.Serialize(new
        {
            kind = "generic_ats_resume",
            layout,
            generation,
            baseVersionId = baseVersion.Id,
            scoreComponents = scoreReport.Components,
            sectionDiffs,
            warnings,
            sources = new[]
            {
                "Jobscan ATS format guidance",
                "Indeed ATS resume template guidance"
            }
        }, JsonOptions);

        if (baseVersion.Document is not null)
            baseVersion.Document.UpdatedAt = now;
        db.ResumeVersions.Add(atsVersion);
        db.ResumeTailoringRuns.Add(new ResumeTailoringRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = atsVersion.Id,
            ProviderModel = generation,
            PromptVersion = "generic-ats-resume-v1",
            ResultJson = JsonSerializer.Serialize(new
            {
                baseVersionId = baseVersion.Id,
                atsVersionId = atsVersion.Id,
                layout,
                score = scoreReport.Score,
                scoreComponents = scoreReport.Components,
                sectionDiffs,
                generation
            }, JsonOptions),
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        return new TailoredResumeResponse(
            atsVersion.Id,
            baseVersion.Id,
            Guid.Empty,
            Guid.Empty,
            atsVersion.Status,
            atsVersion.AtsScore,
            scoreReport.Components,
            sectionDiffs,
            [],
            plainText,
            ResumeDraftBuilder.ExtractSkillsFromVersion(profile, baseVersion),
            [],
            [],
            [],
            warnings,
            ResumeTailoringAnalyzer.ToVersionDetail(atsVersion),
            aiResult is not null);
    }

    private async Task<TailoredResumeGenerationResponse?> TryGenerateWithAiAsync(
        Guid userId,
        CandidateProfile? profile,
        ResumeVersion baseVersion,
        JobTarget target,
        ResumeEvidenceMapDetail evidenceMap,
        TailoringAnalysis analysis,
        string deterministicDraft,
        string layout)
    {
        try
        {
            var response = await ai.GenerateTailoredResumeAsync(userId, new TailoredResumeGenerationInput(
                layout,
                JsonSerializer.Serialize(profile ?? new CandidateProfile { UserId = userId }, JsonOptions),
                JsonSerializer.Serialize(new
                {
                    target.Id,
                    target.Title,
                    target.Company,
                    target.Location,
                    target.Seniority,
                    target.Workplace,
                    target.Summary
                }, JsonOptions),
                baseVersion.PlainText,
                deterministicDraft,
                evidenceMap.Items,
                analysis.IncludedKeywords,
                analysis.AdjacentKeywords,
                analysis.ExcludedKeywords,
                analysis.Checkpoints));
            if (response is null) return null;

            var validationWarnings = ResumeTailoringAnalyzer.ValidateAiDraft(
                response.PlainText,
                analysis,
                string.Join("\n", baseVersion.PlainText, string.Join(", ", profile?.Skills ?? []), profile?.ProfessionalSummary ?? ""));
            if (validationWarnings.Any(x => x.StartsWith("IA rejeitada:", StringComparison.OrdinalIgnoreCase)))
                return null;

            return response with
            {
                Warnings = response.Warnings.Concat(validationWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<TailoredResumeGenerationResponse?> TryGenerateGenericAtsWithAiAsync(
        Guid userId,
        CandidateProfile? profile,
        ResumeVersion baseVersion,
        string deterministicDraft,
        string layout)
    {
        try
        {
            var skills = ResumeDraftBuilder.ExtractSkillsFromVersion(profile, baseVersion);
            var response = await ai.GenerateGenericAtsResumeAsync(userId, new TailoredResumeGenerationInput(
                layout,
                JsonSerializer.Serialize(profile ?? new CandidateProfile { UserId = userId }, JsonOptions),
                JsonSerializer.Serialize(new
                {
                    kind = "generic_ats_resume",
                    targetRole = ResumeTextSanitizer.FirstNonEmpty(profile?.TargetRole, baseVersion.TargetRole)
                }, JsonOptions),
                baseVersion.PlainText,
                deterministicDraft,
                [],
                skills,
                [],
                [],
                []));
            if (response is null) return null;

            var validationWarnings = ValidateGenericAiDraft(response.PlainText, deterministicDraft, skills);
            if (validationWarnings.Any(x => x.StartsWith("IA rejeitada:", StringComparison.OrdinalIgnoreCase)))
                return null;

            return response with
            {
                Warnings = response.Warnings.Concat(validationWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }
        catch
        {
            return null;
        }
    }

    private static string[] ValidateGenericAiDraft(string aiDraft, string deterministicDraft, string[] knownSkills)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(aiDraft))
        {
            warnings.Add("IA rejeitada: texto vazio.");
            return warnings.ToArray();
        }

        if (aiDraft.Length < deterministicDraft.Length * 0.65)
            warnings.Add("IA gerou versao muito curta; revise se evidencias importantes ficaram de fora.");
        if (Regex.IsMatch(aiDraft, @"[│┌┐└┘■●◆★]"))
            warnings.Add("IA rejeitada: adicionou simbolos decorativos.");
        if (aiDraft.Contains("...", StringComparison.Ordinal))
            warnings.Add("IA usou reticencias; revise cortes de conteudo.");

        var missingKnownSkills = knownSkills
            .Where(skill => skill.Length >= 3 && deterministicDraft.Contains(skill, StringComparison.OrdinalIgnoreCase) && !aiDraft.Contains(skill, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToArray();
        if (missingKnownSkills.Length > 0)
            warnings.Add($"IA removeu competencias presentes no curriculo base: {string.Join(", ", missingKnownSkills)}.");

        return warnings.ToArray();
    }

}
