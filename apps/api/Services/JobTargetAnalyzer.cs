using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using static JobPilot.Api.Services.JobTargetTextRules;

namespace JobPilot.Api.Services;

internal static class JobTargetAnalyzer
{
    public static JobRequirementExtractionResponse BuildDeterministicExtraction(ResolvedJobSource source)
    {
        var text = source.Text;
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        var requirements = new List<JobRequirementItem>();
        var order = 0;

        foreach (var tech in KnownTech.Where(tech => ContainsTerm(normalized, tech)))
        {
            requirements.Add(new JobRequirementItem(
                "technology",
                tech,
                LooksMustHave(normalized, tech) ? "must_have" : "nice_to_have",
                EvidenceAround(text, tech),
                NormalizeKeyword(tech),
                [tech]));
            order++;
        }

        if (ContainsAny(normalized, ["api", "apis", "rest", "backend"]))
            requirements.Add(new JobRequirementItem("responsibility", "Desenvolvimento de APIs e integrações", "context", EvidenceAround(text, "API"), "apis-rest", ["APIs", "REST"]));
        if (ContainsAny(normalized, ["testes", "testing", "qa", "qualidade"]))
            requirements.Add(new JobRequirementItem("responsibility", "Testes e qualidade de software", "context", EvidenceAround(text, "testes"), "testes", ["testes", "QA"]));

        var warnings = new List<string>();
        if (requirements.Count == 0) warnings.Add("Poucos requisitos detectados sem IA; revise o texto da vaga.");

        return new JobRequirementExtractionResponse(
            source.Title,
            source.Company,
            source.Location,
            DetectSeniority(normalized),
            DetectWorkplace(normalized),
            BuildSummary(text),
            requirements.DistinctBy(x => x.NormalizedKeyword).Take(30).ToArray(),
            warnings.ToArray(),
            requirements.Count > 0 ? 0.55m : 0.25m);
    }

    public static JobRequirementExtractionResponse EnrichWithDeterministicRequirements(JobRequirementExtractionResponse extraction, ResolvedJobSource source)
    {
        var deterministic = BuildDeterministicExtraction(source);
        if (deterministic.Requirements.Length == 0) return extraction;

        var existing = new HashSet<string>(
            extraction.Requirements.Select(item => NormalizeKeyword(string.IsNullOrWhiteSpace(item.NormalizedKeyword) ? item.Label : item.NormalizedKeyword)),
            StringComparer.OrdinalIgnoreCase);
        var additions = deterministic.Requirements
            .Where(item => existing.Add(NormalizeKeyword(string.IsNullOrWhiteSpace(item.NormalizedKeyword) ? item.Label : item.NormalizedKeyword)))
            .ToArray();
        if (additions.Length == 0) return extraction;

        return extraction with
        {
            Requirements = extraction.Requirements.Concat(additions).Take(50).ToArray()
        };
    }

    public static JobRequirementExtractionResponse NormalizeExtraction(JobRequirementExtractionResponse extraction, ResolvedJobSource source)
    {
        var requirements = extraction.Requirements
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => x with
            {
                Category = NormalizeOneOf(x.Category, ["technology", "responsibility", "experience", "education", "language", "certification", "domain", "soft_skill", "work_model", "other"], "other"),
                Importance = NormalizeOneOf(x.Importance, ["must_have", "nice_to_have", "context"], "context"),
                Label = x.Label.Trim(),
                EvidenceText = TruncateEvidence(x.EvidenceText.Trim()),
                NormalizedKeyword = string.IsNullOrWhiteSpace(x.NormalizedKeyword) ? NormalizeKeyword(x.Label) : NormalizeKeyword(x.NormalizedKeyword),
                ExactPhrases = (x.ExactPhrases ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray()
            })
            .DistinctBy(x => $"{x.Category}:{x.NormalizedKeyword}")
            .Take(50)
            .ToArray();

        var warnings = (extraction.Warnings ?? [])
            .Concat(source.Warnings)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct()
            .ToList();

        // Post-extraction validation warnings
        var reqWithoutEvidence = requirements.Count(x => string.IsNullOrWhiteSpace(x.EvidenceText));
        if (reqWithoutEvidence > 0)
            warnings.Add($"{reqWithoutEvidence} requisito(s) extraido(s) sem trecho de evidencia claro da vaga.");

        if (requirements.Length < 3)
            warnings.Add("Poucos requisitos detectados. Verifique se o texto da vaga esta completo.");

        // Title extraction: prefer first-line detection (most reliable), then LLM extraction, then inference
        var title = "";
        var titleSource = "extracted";

        // First-line detection is the most reliable for well-structured job texts
        var firstLineTitle = ExtractTitleFromFirstLine(source.Text);
        if (!string.IsNullOrWhiteSpace(firstLineTitle))
        {
            title = ResumeTextSanitizer.CleanProfessionalTitle(firstLineTitle);
            titleSource = "first-line";
        }
        else
        {
            // Fall back to LLM-extracted title if first-line failed
            title = FirstNonEmpty(extraction.Title, source.Title);
            if (!string.IsNullOrWhiteSpace(title))
            {
                title = ResumeTextSanitizer.CleanProfessionalTitle(title);
            }
            else
            {
                // Last resort: infer from tech stack
                title = InferTitleFromRequirements(requirements);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titleSource = "inferred";
                }
            }
        }

        // Remove generic "cargo not informed" warning if we have any title
        if (!string.IsNullOrWhiteSpace(title))
        {
            warnings.RemoveAll(w => w.Contains("Cargo nao foi informado", StringComparison.OrdinalIgnoreCase));
            warnings.RemoveAll(w => w.Contains("titulo nao informado", StringComparison.OrdinalIgnoreCase));
        }

        // Only add inferred-title warning when we truly had to infer from tech stack (not first-line)
        if (titleSource == "inferred" && !string.IsNullOrWhiteSpace(title))
        {
            warnings.Add($"Cargo inferido do contexto da vaga: '{title}'. Verifique se esta correto.");
        }

        var textLength = source.Text?.Length ?? 0;
        var isStructured = IsStructuredJobText(source.Text);
        if (textLength < 150 && !isStructured && extraction.Confidence < 0.75m)
            warnings.Add($"Texto da vaga muito curto ({textLength} caracteres). Recomendamos descricoes com pelo menos 300 caracteres para melhor extracao.");
        else if (textLength < 300 && !isStructured)
            warnings.Add($"Texto da vaga curto ({textLength} caracteres) e sem estrutura clara. A extracao pode estar incompleta.");

        // Hybrid confidence calculation
        var hybridConfidence = CalculateHybridConfidence(extraction, requirements, title, warnings, textLength, source.Text);

        return extraction with
        {
            Title = title,
            Company = FirstNonEmpty(extraction.Company, source.Company),
            Location = FirstNonEmpty(extraction.Location, source.Location),
            Seniority = ResolveSeniority(extraction.Seniority, ResumeTextSanitizer.JoinNonEmpty("\n", source.Title, source.Text)),
            Workplace = NormalizeOneOf(extraction.Workplace, ["remote", "hybrid", "onsite", "unknown"], DetectWorkplace(RemoveDiacritics(source.Text ?? "").ToLowerInvariant())),
            Summary = string.IsNullOrWhiteSpace(extraction.Summary) ? BuildSummary(source.Text ?? "") : extraction.Summary.Trim(),
            Requirements = requirements,
            Warnings = warnings.ToArray(),
            Confidence = hybridConfidence
        };
    }

    private static decimal CalculateHybridConfidence(JobRequirementExtractionResponse extraction, JobRequirementItem[] requirements, string? title, List<string> warnings, int textLength, string? sourceText)
    {
        // Start with LLM confidence weighted at 40%
        var rawLlmConfidence = Math.Clamp(extraction.Confidence, 0m, 1m);

        // Detect if this is a high-quality structured text — if so, floor the LLM confidence
        // so that even a slightly conservative LLM doesn't drag down the score
        var evidenceRatio = requirements.Length == 0 ? 0m : (decimal)requirements.Count(r => !string.IsNullOrWhiteSpace(r.EvidenceText)) / requirements.Length;
        var isStructured = IsStructuredJobText(extraction.Summary) || IsStructuredJobText(sourceText);
        var isInferred = warnings.Any(w => w.Contains("inferido", StringComparison.OrdinalIgnoreCase));
        var criticalWarnings = warnings.Where(w =>
            !w.Contains("inferido", StringComparison.OrdinalIgnoreCase) &&
            !w.Contains("Cargo nao foi informado", StringComparison.OrdinalIgnoreCase) &&
            !w.Contains("curto", StringComparison.OrdinalIgnoreCase) &&
            !w.Contains("pequeno", StringComparison.OrdinalIgnoreCase)).ToList();

        // Floor LLM confidence for well-structured, complete extractions
        var llmConfidence = rawLlmConfidence;
        if (isStructured && requirements.Length >= 5 && evidenceRatio >= 0.9m && !string.IsNullOrWhiteSpace(title))
            llmConfidence = Math.Max(llmConfidence, 0.90m);
        else if (isStructured && requirements.Length >= 3 && evidenceRatio >= 0.8m && !string.IsNullOrWhiteSpace(title))
            llmConfidence = Math.Max(llmConfidence, 0.85m);

        var score = llmConfidence * 0.40m;

        // Title bonus: +0.20 if explicitly extracted, +0.18 if first-line detected, +0.10 if inferred
        var hasExplicitTitle = !string.IsNullOrWhiteSpace(extraction.Title);
        if (hasExplicitTitle && !isInferred)
            score += 0.20m;
        else if (!string.IsNullOrWhiteSpace(title) && !isInferred)
            score += 0.18m; // first-line detected without inference warning
        else if (!string.IsNullOrWhiteSpace(title))
            score += 0.10m; // inferred from tech stack

        // Requirements with evidence: up to +0.25
        score += evidenceRatio * 0.25m;

        // Reasonable number of requirements (5-30): +0.12
        if (requirements.Length >= 5 && requirements.Length <= 30)
            score += 0.12m;
        else if (requirements.Length >= 3)
            score += 0.06m;

        // Text length and structure
        if (textLength >= 800)
            score += 0.06m;
        else if (textLength >= 500)
            score += 0.05m;
        else if (textLength >= 300)
            score += isStructured ? 0.04m : 0.02m;

        // Penalty for warnings
        var warningPenalty = criticalWarnings.Count * 0.03m;
        score -= warningPenalty;

        // Penalty for very short text
        if (textLength < 150)
            score -= 0.08m;
        else if (textLength < 300 && !isStructured)
            score -= 0.03m;

        // ─── Floor boosts (guaranteed minimums) ───

        // Base floor: structured text with title, 5+ reqs, 80%+ evidence
        if (!string.IsNullOrWhiteSpace(title) && requirements.Length >= 5 && evidenceRatio >= 0.8m && isStructured)
            score = Math.Max(score, 0.85m);

        // First-line title floor: first-line title, no critical warnings, 90%+ evidence
        if (!string.IsNullOrWhiteSpace(title) && !isInferred && criticalWarnings.Count == 0 && evidenceRatio >= 0.9m)
            score = Math.Max(score, 0.90m);

        // Strong extraction floor: LLM >= 0.85, 10+ reqs, 95%+ evidence, no critical warnings
        if (llmConfidence >= 0.85m && requirements.Length >= 10 && evidenceRatio >= 0.95m && criticalWarnings.Count == 0)
            score = Math.Max(score, 0.92m);

        // Excellent extraction floor: structured, first-line title, 15+ reqs, 98%+ evidence, no warnings
        if (llmConfidence >= 0.85m && !isInferred && requirements.Length >= 15 && evidenceRatio >= 0.98m && criticalWarnings.Count == 0 && isStructured)
            score = Math.Max(score, 0.94m);

        // Perfect extraction floor: structured, first-line title, 20+ reqs, 100% evidence, no warnings
        if (llmConfidence >= 0.85m && !isInferred && requirements.Length >= 20 && evidenceRatio >= 0.99m && criticalWarnings.Count == 0 && isStructured)
            score = Math.Max(score, 0.96m);

        return Math.Clamp(score, 0.15m, 0.98m);
    }

    private static string NormalizeOneOf(string value, string[] allowed, string fallback)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : fallback;
    }

    private static string ResolveSeniority(string extracted, string? sourceText)
    {
        var detected = DetectSeniority(RemoveDiacritics(sourceText ?? "").ToLowerInvariant());
        if (detected != "unknown") return detected;
        return NormalizeOneOf(extracted, ["intern", "junior", "mid", "senior", "lead", "unknown"], "unknown");
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";

}

public sealed record ResolvedJobSource(string Kind, string Text, string Url, string Title, string Company, string Location, Guid? JobLeadId, AiInputAttachment[] Attachments, string[] Warnings);
