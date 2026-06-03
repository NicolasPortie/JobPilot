using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeTailoringService(JobPilotDbContext db, ResumeEvidenceMapService evidenceMapService, IAiChatService ai)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static readonly ResumeLayoutOption[] LayoutOptions =
    [
        new("ats_reverse_chronological", "ATS Reverse Chronological", "Formato geral recomendado: contato, resumo, skills, experiencia em ordem reversa, projetos, formacao e extras.", "Uma coluna, headings tradicionais, fonte padrao, sem tabelas, colunas, imagens, icones, headers ou footers.", true),
        new("ats_classic", "ATS Classic", "Modelo classico e conservador para upload em ATS, com secoes padrao e leitura linear.", "Uma coluna, maxima compatibilidade e secoes previsiveis.", true),
        new("ats_compact", "ATS Compact", "Versao mais enxuta para tentar caber em uma pagina sem reduzir demais a fonte.", "Uma coluna, parseavel, com menos espaco e trechos longos limitados.", true),
        new("ats_skills_first", "ATS Skills First", "Modelo hibrido seguro para perfis tecnicos: skills antes da experiencia, sem colunas.", "Uma coluna; util para tecnologia quando a triagem busca palavras-chave, mantendo experiencia logo depois.", true),
        new("ats_modern_minimal", "ATS Modern Minimal", "Visual mais moderno, ainda sem elementos arriscados para ATS.", "Uma coluna, apenas tipografia/espacamento; sem caixas, tabelas ou graficos.", true),
        new("human_review", "Human Review", "Versao mais confortavel para leitura humana quando o envio nao depender de ATS rigido.", "Ainda exporta texto selecionavel em uma coluna, mas prioriza leitura por recrutador.", false)
    ];

    public async Task<TailoredResumeResponse> TailorAsync(Guid userId, TailorResumeRequest request)
    {
        var layout = NormalizeLayout(request.Layout);
        var baseVersion = await db.ResumeVersions
            .Include(x => x.Document)
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.BaseResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao base de curriculo nao encontrada.");

        var target = await db.JobTargets
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == request.JobTargetId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga alvo nao encontrada.");

        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var evidenceMap = request.EvidenceMapRunId is { } mapId
            ? await evidenceMapService.GetAsync(userId, mapId)
            : await evidenceMapService.BuildAsync(userId, new BuildResumeEvidenceMapRequest(baseVersion.Id, target.Id));
        if (evidenceMap is null || evidenceMap.ResumeVersionId != baseVersion.Id || evidenceMap.JobTargetId != target.Id)
            throw new InvalidOperationException("Mapa de evidencia nao encontrado para esta versao e vaga.");

        var analysis = AnalyzeEvidence(evidenceMap);
        var warnings = BuildWarnings(evidenceMap, analysis);
        var deterministicDraft = BuildPlainText(profile, baseVersion, target, analysis, layout);
        var aiResult = request.UseAi
            ? await TryGenerateWithAiAsync(userId, profile, baseVersion, target, evidenceMap, analysis, deterministicDraft, layout)
            : null;
        var plainText = aiResult?.PlainText ?? deterministicDraft;
        var status = analysis.Checkpoints.Length > 0 || warnings.Length > 0 ? "needs_review" : "ready";
        var scoreReport = BuildScoreReport(evidenceMap, analysis);
        var score = scoreReport.Score;
        var reviewQuestions = BuildReviewQuestions(evidenceMap);
        if (aiResult is not null)
            warnings = warnings.Concat(aiResult.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var now = DateTimeOffset.UtcNow;

        var tailored = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = baseVersion.ResumeDocumentId,
            JobLeadId = target.JobLeadId,
            Title = BuildTitle(target),
            TargetCompany = target.Company,
            TargetRole = target.Title,
            Status = status,
            AtsScore = score,
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };
        tailored.Sections.AddRange(BuildSections(userId, tailored.Id, profile, target, analysis, plainText));
        var sectionDiffs = BuildSectionDiffs(baseVersion, tailored);
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
            ToVersionDetail(tailored));
    }

    public async Task<TailoredResumeResponse> BuildGenericAtsAsync(Guid userId, BuildGenericAtsResumeRequest request)
    {
        var layout = NormalizeLayout(request.Layout);
        var baseVersion = await db.ResumeVersions
            .Include(x => x.Document)
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.BaseResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao base de curriculo nao encontrada.");
        var profile = await db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);

        var plainText = BuildGenericAtsPlainText(profile, baseVersion, layout);
        if (string.IsNullOrWhiteSpace(plainText) || plainText.Length < 120)
            throw new InvalidOperationException("Curriculo base sem conteudo suficiente para gerar uma versao ATS.");

        var now = DateTimeOffset.UtcNow;
        var scoreReport = BuildGenericScoreReport(plainText, baseVersion.Sections);
        var atsVersion = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = baseVersion.ResumeDocumentId,
            Title = $"Curriculo ATS - {LayoutLabel(layout)}",
            TargetCompany = "",
            TargetRole = FirstNonEmpty(baseVersion.TargetRole, profile?.TargetRole),
            Status = scoreReport.Score >= 70 ? "ready" : "needs_review",
            AtsScore = scoreReport.Score,
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };
        atsVersion.Sections.AddRange(BuildGenericSections(userId, atsVersion.Id, profile, baseVersion, plainText));
        var sectionDiffs = BuildSectionDiffs(baseVersion, atsVersion);
        var warnings = BuildGenericWarnings(plainText, baseVersion.Sections);
        atsVersion.AtsReportJson = JsonSerializer.Serialize(new
        {
            kind = "generic_ats_resume",
            layout,
            generation = "backend-deterministic",
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
            ProviderModel = "backend-deterministic",
            PromptVersion = "generic-ats-resume-v1",
            ResultJson = JsonSerializer.Serialize(new
            {
                baseVersionId = baseVersion.Id,
                atsVersionId = atsVersion.Id,
                layout,
                score = scoreReport.Score,
                scoreComponents = scoreReport.Components,
                sectionDiffs,
                generation = "backend-deterministic"
            }, JsonOptions),
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            CreatedAt = now
        });

        await db.SaveChangesAsync();

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
            ExtractSkillsFromVersion(profile, baseVersion),
            [],
            [],
            [],
            warnings,
            ToVersionDetail(atsVersion));
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

            var validationWarnings = ValidateAiDraft(response.PlainText, analysis);
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

    private static string[] ValidateAiDraft(string plainText, TailoringAnalysis analysis)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(plainText) || plainText.Length < 120)
        {
            warnings.Add("IA rejeitada: texto gerado curto demais.");
            return warnings.ToArray();
        }

        var factualText = plainText.Split("PENDENCIAS PARA REVISAO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? plainText;
        foreach (var excluded in analysis.ExcludedKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (ContainsTerm(factualText, excluded))
                warnings.Add($"IA rejeitada: requisito sem evidencia apareceu como fato: {excluded}.");
        }

        foreach (var adjacent in analysis.AdjacentKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (ContainsTerm(factualText, adjacent) && !ContainsAny(factualText, ["relacionad", "proxim", "adjacent", "similar"]))
                warnings.Add($"IA incluiu evidencia adjacente; revise redacao de: {adjacent}.");
        }

        return warnings.ToArray();
    }

    private static TailoringAnalysis AnalyzeEvidence(ResumeEvidenceMapDetail map)
    {
        var included = map.Items
            .Where(x => x.MatchStatus == "proven")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adjacent = map.Items
            .Where(x => x.MatchStatus == "adjacent")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var excluded = map.Items
            .Where(x => x.MatchStatus is "missing" or "unknown")
            .Select(x => x.RequirementLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var checkpoints = map.Items
            .Where(x => x.MatchStatus is "missing" or "unknown" || (x.MatchStatus == "adjacent" && x.RequirementImportance == "must_have"))
            .Select(x => $"{x.RequirementLabel}: {x.Reason}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var provenEvidence = map.Items
            .Where(x => x.MatchStatus == "proven" && !string.IsNullOrWhiteSpace(x.EvidenceText))
            .Select(x => new EvidenceLine(x.RequirementLabel, x.EvidenceText, x.EvidenceSource))
            .ToArray();
        var adjacentEvidence = map.Items
            .Where(x => x.MatchStatus == "adjacent" && !string.IsNullOrWhiteSpace(x.EvidenceText))
            .Select(x => new EvidenceLine(x.RequirementLabel, x.EvidenceText, x.EvidenceSource))
            .ToArray();
        return new TailoringAnalysis(included, adjacent, excluded, checkpoints, provenEvidence, adjacentEvidence);
    }

    private static string[] BuildWarnings(ResumeEvidenceMapDetail map, TailoringAnalysis analysis)
    {
        var warnings = new List<string>(map.Warnings);
        if (analysis.ExcludedKeywords.Length > 0)
            warnings.Add("Requisitos sem evidencia nao foram inseridos como competencia no curriculo direcionado.");
        if (analysis.AdjacentKeywords.Length > 0)
            warnings.Add("Evidencias adjacentes foram descritas de forma conservadora, sem afirmar experiencia direta.");
        if (map.Confidence < 0.65m)
            warnings.Add("Confianca do mapa de evidencia abaixo do ideal; revise antes de aplicar.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BuildPlainText(CandidateProfile? profile, ResumeVersion baseVersion, JobTarget target, TailoringAnalysis analysis, string layout)
    {
        var sb = new StringBuilder();
        Append(sb, profile?.FullName, "Nome completo nao informado");
        Append(sb, JoinNonEmpty(" | ", profile?.Email, profile?.Phone, profile?.City, profile?.Country, profile?.LinkedinUrl, profile?.GithubUrl, profile?.PortfolioUrl));
        Blank(sb);

        Section(sb, "OBJETIVO", FirstNonEmpty(target.Title, profile?.TargetRole, baseVersion.TargetRole, "Posicao de tecnologia"));
        Section(sb, "RESUMO PROFISSIONAL", BuildSummary(profile, target, analysis));

        var skills = MergeSkills(profile?.Skills ?? [], analysis.IncludedKeywords);
        if (skills.Length > 0) Section(sb, "COMPETENCIAS COM EVIDENCIA", string.Join(", ", skills));
        if (analysis.AdjacentKeywords.Length > 0)
            Section(sb, "COMPETENCIAS RELACIONADAS", $"Experiencia relacionada a: {string.Join(", ", analysis.AdjacentKeywords)}.");

        var evidenceLines = analysis.ProvenEvidence
            .Take(layout == "ats_compact" ? 5 : 8)
            .Select(x => $"- {x.Keyword}: {CleanEvidence(x.Text)}")
            .ToArray();
        if (evidenceLines.Length > 0) Section(sb, "EVIDENCIAS RELEVANTES PARA A VAGA", string.Join("\n", evidenceLines));

        var adjacentLines = analysis.AdjacentEvidence
            .Take(5)
            .Select(x => $"- {x.Keyword}: evidencia relacionada encontrada em {x.Source}.")
            .ToArray();
        if (adjacentLines.Length > 0) Section(sb, "PONTOS RELACIONADOS", string.Join("\n", adjacentLines));

        var baseText = CleanBaseText(baseVersion.PlainText);
        if (!string.IsNullOrWhiteSpace(baseText))
            Section(sb, "HISTORICO BASE", baseText);

        if (analysis.Checkpoints.Length > 0)
            Section(sb, "PENDENCIAS PARA REVISAO", string.Join("\n", analysis.Checkpoints.Select(x => $"- {x}")));

        return sb.ToString().Trim();
    }

    private static List<ResumeSection> BuildSections(Guid userId, Guid versionId, CandidateProfile? profile, JobTarget target, TailoringAnalysis analysis, string plainText)
    {
        var sections = new List<ResumeSection>();
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            fullName = profile?.FullName ?? "",
            email = profile?.Email ?? "",
            phone = profile?.Phone ?? "",
            city = profile?.City ?? "",
            country = profile?.Country ?? "",
            linkedinUrl = profile?.LinkedinUrl ?? "",
            githubUrl = profile?.GithubUrl ?? "",
            portfolioUrl = profile?.PortfolioUrl ?? ""
        });
        AddSection(sections, userId, versionId, "target", "Vaga alvo", 20, new { target.Id, target.Title, target.Company, target.Location, target.Seniority, target.Workplace });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { proven = analysis.IncludedKeywords, adjacent = analysis.AdjacentKeywords, excluded = analysis.ExcludedKeywords });
        AddSection(sections, userId, versionId, "checkpoints", "Pendencias", 80, new { items = analysis.Checkpoints });
        AddSection(sections, userId, versionId, "plain_text", "Texto ATS", 90, new { text = plainText });
        return sections;
    }

    private static ResumeSectionDiff[] BuildSectionDiffs(ResumeVersion baseVersion, ResumeVersion tailoredVersion)
    {
        var before = baseVersion.Sections
            .GroupBy(section => section.SectionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).First(), StringComparer.OrdinalIgnoreCase);
        var after = tailoredVersion.Sections
            .GroupBy(section => section.SectionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).First(), StringComparer.OrdinalIgnoreCase);

        var keys = before.Keys
            .Concat(after.Keys)
            .Append("plain_text")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SectionDiffOrder)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keys.Select(key =>
            {
                before.TryGetValue(key, out var beforeSection);
                after.TryGetValue(key, out var afterSection);
                var beforeText = key.Equals("plain_text", StringComparison.OrdinalIgnoreCase)
                    ? baseVersion.PlainText
                    : SectionPreview(beforeSection);
                var afterText = key.Equals("plain_text", StringComparison.OrdinalIgnoreCase)
                    ? tailoredVersion.PlainText
                    : SectionPreview(afterSection);
                var status = SectionDiffStatus(beforeText, afterText);
                var title = FirstNonEmpty(afterSection?.Title, beforeSection?.Title, SectionDiffTitle(key));
                return new ResumeSectionDiff(key, title, status, Preview(beforeText), Preview(afterText), afterSection?.Id);
            })
            .Where(diff => diff.Status != "unchanged" || diff.SectionType == "plain_text")
            .ToArray();
    }

    private static ResumeReviewQuestion[] BuildReviewQuestions(ResumeEvidenceMapDetail map) =>
        map.Items
            .Where(x => x.MatchStatus == "unknown")
            .OrderBy(x => x.SortOrder)
            .Select(x => new ResumeReviewQuestion(
                x.JobRequirementId,
                x.RequirementLabel,
                x.RequirementCategory,
                x.RequirementImportance,
                ReviewQuestionText(x),
                x.Reason))
            .ToArray();

    private static string ReviewQuestionText(ResumeRequirementEvidenceDto item)
    {
        var label = item.RequirementLabel.Trim();
        return item.RequirementCategory switch
        {
            "language" => $"Voce tem proficiencia real em {label}? Se sim, qual nivel pode comprovar?",
            "certification" => $"Voce possui a certificacao {label} ou equivalente comprovavel?",
            "education" => $"Voce possui formacao ou curso comprovavel relacionado a {label}?",
            "domain" => $"Voce tem experiencia real no dominio {label}? Em qual contexto?",
            _ => $"Voce possui experiencia real e comprovavel com {label}?"
        };
    }

    private static string SectionPreview(ResumeSection? section) =>
        section is null ? "" : ExtractTextFromJson(section.ContentJson);

    private static string ExtractTextFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var document = JsonDocument.Parse(json);
            var parts = new List<string>();
            CollectJsonText(document.RootElement, parts);
            return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        }
        catch
        {
            return json;
        }
    }

    private static void CollectJsonText(JsonElement element, List<string> parts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                parts.Add(element.GetString() ?? "");
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectJsonText(item, parts);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) CollectJsonText(property.Value, parts);
                break;
        }
    }

    private static string SectionDiffStatus(string beforeText, string afterText)
    {
        var before = NormalizeForDiff(beforeText);
        var after = NormalizeForDiff(afterText);
        if (string.IsNullOrWhiteSpace(before) && !string.IsNullOrWhiteSpace(after)) return "added";
        if (!string.IsNullOrWhiteSpace(before) && string.IsNullOrWhiteSpace(after)) return "removed";
        return before.Equals(after, StringComparison.OrdinalIgnoreCase) ? "unchanged" : "changed";
    }

    private static int SectionDiffOrder(string sectionType) => sectionType.ToLowerInvariant() switch
    {
        "identity" => 10,
        "target" => 20,
        "summary" => 30,
        "skills" => 40,
        "experience" => 50,
        "projects" => 60,
        "education" => 70,
        "certifications" => 75,
        "languages" => 78,
        "checkpoints" => 80,
        "plain_text" => 90,
        "raw_import" => 95,
        _ => 100
    };

    private static string SectionDiffTitle(string sectionType) => sectionType.ToLowerInvariant() switch
    {
        "identity" => "Identidade",
        "target" => "Vaga alvo",
        "summary" => "Resumo",
        "skills" => "Competencias",
        "checkpoints" => "Pendencias",
        "plain_text" => "Texto ATS",
        "raw_import" => "Texto importado",
        _ => sectionType
    };

    private static string NormalizeForDiff(string value) =>
        string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string Preview(string value)
    {
        var normalized = NormalizeForDiff(value);
        return normalized.Length <= 260 ? normalized : $"{normalized[..260].Trim()}...";
    }

    private static ScoreReport BuildScoreReport(ResumeEvidenceMapDetail map, TailoringAnalysis analysis)
    {
        var total = Math.Max(1, map.Items.Length);
        var proven = map.Items.Count(x => x.MatchStatus == "proven");
        var adjacent = map.Items.Count(x => x.MatchStatus == "adjacent");
        var blocked = map.Items.Count(x => x.MatchStatus is "missing" or "unknown");
        var mustHaveBlocked = map.Items.Count(x => x.RequirementImportance == "must_have" && x.MatchStatus is "missing" or "unknown");

        var evidence = ClampScore((int)Math.Round(map.EvidenceScore * 0.45m), 45);
        var coverage = ClampScore((int)Math.Round((decimal)proven / total * 20m), 20);
        var related = ClampScore((int)Math.Round((decimal)adjacent / total * 10m), 10);
        var confidence = ClampScore((int)Math.Round(map.Confidence * 10m), 10);
        var riskPenalty = Math.Min(15, blocked * 3 + mustHaveBlocked * 4 + analysis.Checkpoints.Length);
        var readiness = ClampScore(15 - riskPenalty, 15);

        var components = new[]
        {
            Component("evidence", "Evidencia direta", evidence, 45, $"Mapa de evidencia geral: {map.EvidenceScore}/100."),
            Component("coverage", "Cobertura de requisitos", coverage, 20, $"{proven} de {total} requisito(s) com evidencia direta."),
            Component("related", "Aderencia relacionada", related, 10, $"{adjacent} requisito(s) com evidencia adjacente, sem afirmar como experiencia direta."),
            Component("confidence", "Confianca da analise", confidence, 10, $"Confianca do mapa: {map.Confidence:0.00}."),
            Component("readiness", "Risco de revisao", readiness, 15, $"{blocked} requisito(s) ausente(s)/desconhecido(s), {mustHaveBlocked} must-have bloqueado(s), {analysis.Checkpoints.Length} checkpoint(s).")
        };

        return new ScoreReport(components.Sum(x => x.Score), components);
    }

    private static ResumeScoreComponent Component(string key, string label, int score, int maxScore, string reason) =>
        new(key, label, score, maxScore, ScoreStatus(score, maxScore), reason);

    private static int ClampScore(int score, int maxScore) => Math.Clamp(score, 0, maxScore);

    private static string ScoreStatus(int score, int maxScore)
    {
        if (maxScore <= 0) return "neutral";
        var ratio = (decimal)score / maxScore;
        return ratio >= 0.75m ? "good" : ratio >= 0.45m ? "warn" : "bad";
    }

    private static ResumeVersionDetail ToVersionDetail(ResumeVersion version) =>
        new(
            version.Id,
            version.ResumeDocumentId,
            version.Title,
            version.Status,
            version.TargetRole,
            version.TargetCompany,
            version.AtsScore,
            version.PlainText,
            version.Sections
                .OrderBy(section => section.SortOrder)
                .Select(section => new ResumeSectionDto(section.Id, section.SectionType, section.Title, section.SortOrder, section.ContentJson))
                .ToArray());

    private static void AddSection(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, object content) =>
        sections.Add(new ResumeSection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = versionId,
            SectionType = type,
            Title = title,
            SortOrder = order,
            ContentJson = JsonSerializer.Serialize(content, JsonOptions)
        });

    private static string BuildSummary(CandidateProfile? profile, JobTarget target, TailoringAnalysis analysis)
    {
        var baseSummary = FirstNonEmpty(profile?.ProfessionalSummary, $"Profissional com foco em {FirstNonEmpty(target.Title, "tecnologia")}.");
        var proven = analysis.IncludedKeywords.Length > 0
            ? $" Evidencias diretas para esta vaga: {string.Join(", ", analysis.IncludedKeywords.Take(8))}."
            : "";
        return $"{baseSummary.Trim()}{proven}".Trim();
    }

    private static string BuildTitle(JobTarget target)
    {
        var role = FirstNonEmpty(target.Title, "vaga alvo");
        var company = FirstNonEmpty(target.Company);
        return string.IsNullOrWhiteSpace(company)
            ? $"Curriculo direcionado - {role}"
            : $"Curriculo direcionado - {role} - {company}";
    }

    private static string BuildGenericAtsPlainText(CandidateProfile? profile, ResumeVersion baseVersion, string layout)
    {
        var sb = new StringBuilder();
        var sections = baseVersion.Sections.OrderBy(x => x.SortOrder).ToList();
        var identity = SectionObject(sections, "identity");
        var fullName = FirstNonEmpty(profile?.FullName, JsonValue(identity, "fullName"), JsonValue(identity, "FullName"), "Nome nao informado");
        Append(sb, fullName);
        Append(sb, BuildContactLine(profile, identity));
        Append(sb, JoinUniqueNonEmpty(" | ",
            profile?.LinkedinUrl, JsonValue(identity, "linkedinUrl"), JsonValue(identity, "LinkedinUrl"),
            profile?.GithubUrl, JsonValue(identity, "githubUrl"), JsonValue(identity, "GithubUrl"),
            profile?.PortfolioUrl, JsonValue(identity, "portfolioUrl"), JsonValue(identity, "PortfolioUrl")));
        Blank(sb);

        var targetRole = FirstNonEmpty(baseVersion.TargetRole, profile?.TargetRole);
        if (!string.IsNullOrWhiteSpace(targetRole))
            Section(sb, "OBJETIVO", targetRole);

        var summary = CleanSummaryText(FirstNonEmpty(profile?.ProfessionalSummary, SectionText(sections, "summary")), profile, identity);
        if (!string.IsNullOrWhiteSpace(summary))
            Section(sb, "RESUMO PROFISSIONAL", LimitText(summary, layout == "ats_compact" ? 520 : 800));

        var skills = ExtractSkillsFromVersion(profile, baseVersion);
        if (skills.Length > 0)
            Section(sb, "COMPETENCIAS", string.Join(", ", skills));

        if (layout == "ats_skills_first")
        {
            AppendSectionItems(sb, "PROJETOS", SectionItems(sections, "projects"), layout);
            AppendSectionItems(sb, "EXPERIENCIA PROFISSIONAL", SectionItems(sections, "experience"), layout);
        }
        else
        {
            AppendSectionItems(sb, "EXPERIENCIA PROFISSIONAL", SectionItems(sections, "experience"), layout);
            AppendSectionItems(sb, "PROJETOS", SectionItems(sections, "projects"), layout);
        }

        AppendSectionItems(sb, "FORMACAO", SectionItems(sections, "education"), layout);
        AppendSectionItems(sb, "CERTIFICACOES", SectionItems(sections, "certifications"), layout);
        AppendSectionItems(sb, "IDIOMAS", SectionItems(sections, "languages"), layout);

        AppendFallbackSections(sb, sections, baseVersion.PlainText, layout);

        return sb.ToString().Trim();
    }

    private static List<ResumeSection> BuildGenericSections(Guid userId, Guid versionId, CandidateProfile? profile, ResumeVersion baseVersion, string plainText)
    {
        var sections = new List<ResumeSection>();
        var skills = ExtractSkillsFromVersion(profile, baseVersion);
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            fullName = profile?.FullName ?? "",
            email = profile?.Email ?? "",
            phone = profile?.Phone ?? "",
            city = profile?.City ?? "",
            country = profile?.Country ?? "",
            linkedinUrl = profile?.LinkedinUrl ?? "",
            githubUrl = profile?.GithubUrl ?? "",
            portfolioUrl = profile?.PortfolioUrl ?? ""
        });
        var identity = SectionObject(baseVersion.Sections, "identity");
        AddSection(sections, userId, versionId, "summary", "Resumo", 20, new { text = CleanSummaryText(FirstNonEmpty(profile?.ProfessionalSummary, SectionText(baseVersion.Sections, "summary")), profile, identity) });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { items = skills });
        AddSection(sections, userId, versionId, "plain_text", "Texto ATS", 90, new { text = plainText });
        return sections;
    }

    private static string[] BuildGenericWarnings(string plainText, IEnumerable<ResumeSection> sections)
    {
        var warnings = new List<string>();
        if (!sections.Any(x => x.SectionType == "experience"))
            warnings.Add("Secao de experiencia nao foi encontrada de forma estruturada; revise o curriculo base.");
        if (!sections.Any(x => x.SectionType == "skills"))
            warnings.Add("Secao de competencias nao foi encontrada de forma estruturada; revise palavras-chave reais.");
        if (plainText.Length < 600)
            warnings.Add("Curriculo ATS geral ficou curto; revise se experiencias, projetos e formacao foram importados.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static ScoreReport BuildGenericScoreReport(string plainText, IEnumerable<ResumeSection> sections)
    {
        var sectionList = sections.ToList();
        var structure = 0;
        if (sectionList.Any(x => x.SectionType == "identity")) structure += 10;
        if (sectionList.Any(x => x.SectionType == "summary")) structure += 10;
        if (sectionList.Any(x => x.SectionType == "skills")) structure += 15;
        if (sectionList.Any(x => x.SectionType == "experience")) structure += 20;
        if (sectionList.Any(x => x.SectionType is "education" or "projects")) structure += 10;

        var parseSafety = 20;
        if (plainText.Contains('\t')) parseSafety -= 5;
        if (System.Text.RegularExpressions.Regex.IsMatch(plainText, @"[│┌┐└┘■●◆★]")) parseSafety -= 8;
        var completeness = plainText.Length >= 1200 ? 15 : plainText.Length >= 700 ? 10 : 5;
        var components = new[]
        {
            Component("structure", "Estrutura ATS", Math.Clamp(structure, 0, 65), 65, "Secoes padrao detectadas e reordenadas em fluxo linear."),
            Component("parse_safety", "Seguranca de parsing", Math.Clamp(parseSafety, 0, 20), 20, "Uma coluna, texto simples, sem tabelas ou elementos graficos adicionados."),
            Component("completeness", "Completude", completeness, 15, "Volume de conteudo disponivel no curriculo base.")
        };
        return new ScoreReport(components.Sum(x => x.Score), components);
    }

    private static void AppendSectionItems(StringBuilder sb, string title, List<Dictionary<string, string>> items, string layout)
    {
        if (items.Count == 0) return;
        var lines = new List<string>();
        foreach (var item in items.Take(layout == "ats_compact" ? 4 : 8))
        {
            var heading = FirstNonEmpty(
                JoinNonEmpty(" - ", FirstNonEmpty(Value(item, "role"), Value(item, "degree"), Value(item, "name"), Value(item, "language")), FirstNonEmpty(Value(item, "company"), Value(item, "institution"), Value(item, "issuer"), Value(item, "level"))),
                Value(item, "title"));
            var period = FirstNonEmpty(Value(item, "period"), Value(item, "year"));
            if (!string.IsNullOrWhiteSpace(heading))
                lines.Add(string.IsNullOrWhiteSpace(period) ? heading : $"{heading} | {period}");
            var description = CleanItemText(FirstNonEmpty(Value(item, "description"), Value(item, "summary"), Value(item, "details")));
            if (!string.IsNullOrWhiteSpace(description))
            {
                foreach (var line in description.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(layout == "ats_compact" ? 3 : 6))
                    lines.Add(line.StartsWith("-") ? line : $"- {line}");
            }
            else
            {
                var fallback = string.Join(", ", item.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).Select(pair => CleanItemText(pair.Value)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                if (!string.IsNullOrWhiteSpace(fallback) && fallback != heading)
                    lines.Add($"- {fallback}");
            }
        }
        Section(sb, title, string.Join("\n", lines));
    }

    private static string[] ExtractSkillsFromVersion(CandidateProfile? profile, ResumeVersion version)
    {
        var values = new List<string>();
        values.AddRange(profile?.Skills ?? []);
        values.AddRange(SectionArray(version.Sections, "skills", "items"));
        values.AddRange(SectionArray(version.Sections, "skills", "skills"));
        return values
            .SelectMany(x => x.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(CleanSkill)
            .Where(IsValidSkill)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AppendFallbackSections(StringBuilder sb, List<ResumeSection> sections, string basePlainText, string layout)
    {
        if (string.IsNullOrWhiteSpace(basePlainText)) return;
        var max = layout == "ats_compact" ? 900 : 1400;

        if (!sections.Any(x => x.SectionType == "experience"))
        {
            var experience = ExtractFallbackSection(basePlainText,
                ["EXPERIENCIA PROFISSIONAL", "EXPERIÊNCIA PROFISSIONAL", "EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "WORK EXPERIENCE"],
                ["PROJETOS", "PROJECTS", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max);
            Section(sb, "EXPERIENCIA PROFISSIONAL", experience);
        }

        if (!sections.Any(x => x.SectionType == "projects"))
        {
            var projects = ExtractFallbackSection(basePlainText,
                ["PROJETOS", "PROJECTS"],
                ["EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max / 2);
            Section(sb, "PROJETOS", projects);
        }

        if (!sections.Any(x => x.SectionType == "education"))
        {
            var education = ExtractFallbackSection(basePlainText,
                ["FORMACAO E CERTIFICACOES", "FORMAÇÃO E CERTIFICAÇÕES", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "EDUCATION"],
                ["EXPERIENCIA", "EXPERIÊNCIA", "PROJETOS", "PROJECTS", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max / 2);
            Section(sb, "FORMACAO", education);
        }
    }

    private static string ExtractFallbackSection(string source, string[] startLabels, string[] stopLabels, int maxLength)
    {
        var normalized = NormalizeImportedResumeText(source);
        var start = FindLabel(normalized, startLabels, 0);
        if (start.Index < 0) return "";

        var contentStart = start.Index + start.Label.Length;
        var stop = FindLabel(normalized, stopLabels, contentStart);
        var length = stop.Index >= 0 ? stop.Index - contentStart : normalized.Length - contentStart;
        if (length <= 0) return "";

        var content = normalized.Substring(contentStart, length);
        content = Regex.Replace(content, @"(?i)\b(TEXTO IMPORTADO|CONTEUDO DO CURRICULO BASE|CONTEÚDO DO CURRÍCULO BASE)\b", " ");
        content = CleanItemText(content);
        content = Regex.Replace(content, @"(?im)^\s*DE DESTAQUE\s*$\n?", "");
        if (content.Length < 20) return "";
        return LimitText(content, maxLength);
    }

    private static (int Index, string Label) FindLabel(string value, string[] labels, int startAt)
    {
        if (startAt < 0 || startAt >= value.Length) return (-1, "");
        var bestIndex = -1;
        var bestLabel = "";
        foreach (var label in labels)
        {
            var match = Regex.Match(value[startAt..], $@"(?i)(^|\n)({Regex.Escape(label)})(\s|:|\n)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            var index = startAt + match.Groups[2].Index;
            if (bestIndex >= 0 && index >= bestIndex) continue;
            bestIndex = index;
            bestLabel = match.Groups[2].Value;
        }
        return (bestIndex, bestLabel);
    }

    private static string NormalizeImportedResumeText(string value)
    {
        var text = (value ?? "").Replace("\r", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\b(OBJETIVO|RESUMO PROFISSIONAL|PERFIL PROFISSIONAL|RESUMO|COMPETENCIAS TECNICAS|COMPETÊNCIAS TÉCNICAS|COMPETENCIAS|COMPETÊNCIAS|SKILLS|EXPERIENCIA PROFISSIONAL|EXPERIÊNCIA PROFISSIONAL|EXPERIENCIA|EXPERIÊNCIA|PROJETOS|FORMACAO E CERTIFICACOES|FORMAÇÃO E CERTIFICAÇÕES|FORMACAO|FORMAÇÃO|EDUCACAO|EDUCAÇÃO|CERTIFICACOES|CERTIFICAÇÕES|IDIOMAS|TEXTO IMPORTADO)\b", "\n$1\n");
        text = Regex.Replace(text, @"\n{2,}", "\n");
        return text.Trim();
    }

    private static string CleanSummaryText(string value, CandidateProfile? profile, JsonElement? identity)
    {
        var text = CleanItemText(value);
        if (string.IsNullOrWhiteSpace(text)) return "";

        foreach (var heading in new[] { "PERFIL PROFISSIONAL", "RESUMO PROFISSIONAL", "RESUMO" })
        {
            var index = text.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                text = text[(index + heading.Length)..].Trim();
        }

        var stop = Regex.Match(text, @"(?i)\b(COMPETENCIAS|COMPETÊNCIAS|SKILLS|PROJETOS|FORMACAO|FORMAÇÃO|EDUCACAO|EDUCAÇÃO|CERTIFICACOES|CERTIFICAÇÕES|IDIOMAS|TEXTO IMPORTADO)\b");
        if (stop.Success)
            text = text[..stop.Index].Trim();

        foreach (var valueToRemove in new[]
        {
            profile?.FullName,
            profile?.Email,
            profile?.Phone,
            profile?.City,
            profile?.Country,
            JsonValue(identity, "fullName"),
            JsonValue(identity, "email"),
            JsonValue(identity, "phone"),
            JsonValue(identity, "city"),
            JsonValue(identity, "country")
        })
        {
            if (!string.IsNullOrWhiteSpace(valueToRemove))
                text = text.Replace(valueToRemove, " ", StringComparison.OrdinalIgnoreCase);
        }

        text = Regex.Replace(text, @"\S+@\S+", " ");
        text = Regex.Replace(text, @"\b(?:LinkedIn|GitHub|Portfolio|Portifolio)\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?:\+?\d[\d\s().-]{7,}\d)", " ");
        text = Regex.Replace(text, @"\s*\|\s*", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '|', '-', ',', '.');
        return text;
    }

    private static string CleanItemText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Replace("\r", "\n").Replace("â€¢", "\n- ").Replace("•", "\n- ").Replace("●", "\n- ").Replace("▪", "\n- ").Replace("–", "-").Replace("—", "-");
        text = Regex.Replace(text, @"(?<=[.!?])\s+([A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}\d\s&./+-]{2,80}\s-\s[^\n|]{2,90}\|\s(?:Jan|Fev|Mar|Abr|Mai|Jun|Jul|Ago|Set|Out|Nov|Dez)/\d{4})", "\n$1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string CleanSkill(string value)
    {
        var text = CleanItemText(value);
        text = Regex.Replace(text, @"(?i)\b(COMPETENCIAS|COMPETÊNCIAS|SKILLS|LINGUAGENS|FRAMEWORKS|BANCO DE DADOS|ARQUITETURA|FERRAMENTAS)\b[:\s-]*", "");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '|', '-', ',', ':');
        return text;
    }

    private static bool IsValidSkill(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > 55) return false;
        if (value.Contains('@')) return false;
        if (Regex.IsMatch(value, @"\d{4,}")) return false;
        if (Regex.IsMatch(value, @"(?i)\b(Nicolas|contato|telefone|penapolis|brazil|linkedin|github|portfolio|perfil profissional|objetivo|resumo)\b")) return false;
        return true;
    }

    private static JsonElement? SectionObject(IEnumerable<ResumeSection> sections, string type)
    {
        var section = sections.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.SectionType.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (section is null || string.IsNullOrWhiteSpace(section.ContentJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(section.ContentJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static List<Dictionary<string, string>> SectionItems(IEnumerable<ResumeSection> sections, string type)
    {
        var root = SectionObject(sections, type);
        if (root is null) return [];
        var element = root.Value;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return items.EnumerateArray().Select(JsonObjectToDictionary).Where(x => x.Count > 0).ToList();
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(JsonObjectToDictionary).Where(x => x.Count > 0).ToList();
        return [JsonObjectToDictionary(element)];
    }

    private static Dictionary<string, string> JsonObjectToDictionary(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return new Dictionary<string, string> { ["description"] = element.GetString() ?? "" };
        if (element.ValueKind != JsonValueKind.Object) return [];
        return element.EnumerateObject()
            .ToDictionary(property => property.Name, property => JsonElementToText(property.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] SectionArray(IEnumerable<ResumeSection> sections, string type, string propertyName)
    {
        var root = SectionObject(sections, type);
        if (root is null) return [];
        if (root.Value.ValueKind == JsonValueKind.Object && root.Value.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Array)
                return property.EnumerateArray().Select(JsonElementToText).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (property.ValueKind == JsonValueKind.String)
                return [property.GetString() ?? ""];
        }
        return [];
    }

    private static string SectionText(IEnumerable<ResumeSection> sections, string type)
    {
        var root = SectionObject(sections, type);
        if (root is null) return "";
        if (root.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "text", "summary", "description" })
                if (root.Value.TryGetProperty(key, out var property))
                    return JsonElementToText(property);
        }
        return JsonElementToText(root.Value);
    }

    private static string JsonValue(JsonElement? element, string key)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object) return "";
        return element.Value.TryGetProperty(key, out var property) ? JsonElementToText(property) : "";
    }

    private static string JsonElementToText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(JsonElementToText).Where(x => !string.IsNullOrWhiteSpace(x))),
            JsonValueKind.Object => string.Join(", ", element.EnumerateObject().Select(property => JsonElementToText(property.Value)).Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => ""
        };
    }

    private static string Value(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : "";

    private static string LimitText(string value, int maxLength)
    {
        var cleaned = NormalizeForResumeSection(value);
        return cleaned.Length <= maxLength ? cleaned : $"{cleaned[..maxLength].Trim()}...";
    }

    private static string NormalizeForResumeSection(string value) =>
        string.Join("\n", (value ?? "")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]{2,}", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)))
            .Trim();

    private static string LayoutLabel(string layout) =>
        LayoutOptions.FirstOrDefault(x => x.Key == layout)?.Label ?? "ATS Classic";

    private static string CleanEvidence(string value)
    {
        var cleaned = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return cleaned.Length <= 240 ? cleaned : $"{cleaned[..240].Trim()}...";
    }

    private static string CleanBaseText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var cleaned = value.Trim();
        return cleaned.Length <= 1800 ? cleaned : $"{cleaned[..1800].Trim()}...";
    }

    private static string[] MergeSkills(string[] profileSkills, string[] provenKeywords) =>
        profileSkills
            .Concat(provenKeywords)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeLayout(string value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace("-", "_");
        return LayoutOptions.Any(x => x.Key == normalized) ? normalized : "ats_reverse_chronological";
    }

    private static void Section(StringBuilder sb, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        Append(sb, title);
        foreach (var line in content.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
            Append(sb, line);
        Blank(sb);
    }

    private static void Append(StringBuilder sb, string? value, string fallback = "")
    {
        var text = FirstNonEmpty(value, fallback);
        if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
    }

    private static void Blank(StringBuilder sb) => sb.AppendLine();

    private static string BuildContactLine(CandidateProfile? profile, JsonElement? identity)
    {
        var values = new List<string>();
        AddUnique(values, profile?.Email);
        AddUnique(values, JsonValue(identity, "email"));
        AddUnique(values, JsonValue(identity, "Email"));
        AddUnique(values, profile?.Phone, NormalizePhone);
        AddUnique(values, JsonValue(identity, "phone"), NormalizePhone);
        AddUnique(values, JsonValue(identity, "Phone"), NormalizePhone);
        AddUnique(values, profile?.City);
        AddUnique(values, JsonValue(identity, "city"));
        AddUnique(values, JsonValue(identity, "City"));
        AddUnique(values, profile?.Country);
        AddUnique(values, JsonValue(identity, "country"));
        AddUnique(values, JsonValue(identity, "Country"));
        return string.Join(" | ", values);
    }

    private static void AddUnique(List<string> values, string? value, Func<string, string>? keySelector = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var cleaned = value.Trim();
        var key = keySelector?.Invoke(cleaned) ?? cleaned;
        if (string.IsNullOrWhiteSpace(key)) return;
        var duplicateIndex = values.FindIndex(existing => string.Equals(keySelector?.Invoke(existing) ?? existing, key, StringComparison.OrdinalIgnoreCase));
        if (duplicateIndex >= 0)
        {
            if (keySelector is not null && cleaned.Count(char.IsPunctuation) > values[duplicateIndex].Count(char.IsPunctuation))
                values[duplicateIndex] = cleaned;
            return;
        }
        values.Add(cleaned);
    }

    private static string NormalizePhone(string value)
    {
        var digits = Regex.Replace(value ?? "", @"\D", "");
        if (digits.Length > 11 && digits.StartsWith("55", StringComparison.Ordinal)) digits = digits[2..];
        return digits;
    }

    private static string JoinUniqueNonEmpty(string separator, params string?[] values) =>
        string.Join(separator, values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string JoinNonEmpty(string separator, params string?[] values) =>
        string.Join(separator, values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";

    private static bool ContainsAny(string value, string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsTerm(string value, string term)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(term)) return false;
        return value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TailoringAnalysis(string[] IncludedKeywords, string[] AdjacentKeywords, string[] ExcludedKeywords, string[] Checkpoints, EvidenceLine[] ProvenEvidence, EvidenceLine[] AdjacentEvidence);
    private sealed record EvidenceLine(string Keyword, string Text, string Source);
    private sealed record ScoreReport(int Score, ResumeScoreComponent[] Components);
}
