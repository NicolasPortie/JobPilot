using System.Text.Json;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed record SearchDecisionPrompt(string System, string User, object Schema);

public static class SearchDecisionHarness
{
    public static readonly object JobFitJsonSchema = new
    {
        type = "object",
        properties = new
        {
            decision = new { type = "string", @enum = new[] { "queue", "ignore", "review" } },
            fitScore = new { type = "integer", minimum = 0, maximum = 100 },
            reason = new { type = "string" },
            detectedSeniority = new { type = "string", @enum = new[] { "intern", "junior", "mid", "senior", "lead", "unknown" } },
            detectedWorkplace = new { type = "string", @enum = new[] { "remote", "hybrid", "onsite", "unknown" } },
            applyType = new { type = "string", @enum = new[] { "easy_apply", "external_form", "unknown" } },
            strengths = new { type = "array", items = new { type = "string" } },
            risks = new { type = "array", items = new { type = "string" } },
            confidence = new { type = "number", minimum = 0, maximum = 1 }
        },
        required = new[] { "decision", "fitScore", "reason", "detectedSeniority", "detectedWorkplace", "applyType", "strengths", "risks", "confidence" },
        additionalProperties = false
    };

    public static SearchDecisionPrompt BuildJobFitPrompt(JobLead lead, CandidateProfile? profile, JobFilter filter, JsonSerializerOptions jsonOptions)
    {
        var system = """
            You are JobPilot's job-search decision harness.
            Decide if a discovered job should be queued for application, ignored, or held for human review.
            Return only JSON matching the schema.
            Be strict: junior candidate should not be queued for senior, lead, manager, or ambiguous high-experience roles unless explicitly allowed by filters.
            Queue only when the job is plausibly aligned with the candidate profile and the configured filters.
            Treat configured allowed role keywords as the source of truth for acceptable role families.
            Use review when evidence is incomplete, contradictory, or confidence is low.
            """;

        var prompt = $$"""
            Candidate profile:
            {{JsonSerializer.Serialize(new
            {
                profile?.TargetRole,
                profile?.Seniority,
                Skills = profile?.Skills ?? [],
                profile?.City,
                profile?.Country,
                profile?.ProfessionalSummary
            }, jsonOptions)}}

            Search filters:
            {{JsonSerializer.Serialize(new
            {
                filter.AllowedSeniorities,
                filter.BlockedSeniorities,
                filter.AllowedRoleKeywords,
                filter.BlockedRoleKeywords,
                filter.AllowedWorkplaceTypes,
                filter.BlockedWorkplaceTypes,
                filter.AllowedApplyTypes,
                filter.MinScore
            }, jsonOptions)}}

            Discovered job:
            {{JsonSerializer.Serialize(new
            {
                lead.Title,
                lead.Company,
                lead.Location,
                lead.Description,
                lead.Url,
                lead.Platform,
                lead.IsRemote,
                RuleScore = lead.Score,
                RuleReason = lead.ScoreReason
            }, jsonOptions)}}

            Decision rules:
            - decision=queue only when fitScore >= 60 and confidence >= 0.60.
            - roles matching AllowedRoleKeywords may include ASP.NET/.NET backend, full stack, QA/test automation, and ASP.NET/.NET architecture when configured.
            - decision=ignore when the role is clearly outside target, explicitly too senior, explicitly onsite/hybrid while remote-only, non-technical for a technical profile, or likely fake/demo.
            - decision=review when the job might fit but important evidence is missing, including missing junior wording or missing remote badge.
            - reason must be short, concrete, and audit-friendly.
            """;

        return new SearchDecisionPrompt(system, prompt, JobFitJsonSchema);
    }

    public static JobFitReviewResponse Validate(JobFitReviewResponse review, JobLead lead, JobFilter filter)
    {
        var decision = NormalizeDecision(review.Decision ?? "");
        var fitScore = Math.Clamp(review.FitScore, 0, 100);
        var confidence = Math.Clamp(review.Confidence, 0m, 1m);
        var reason = string.IsNullOrWhiteSpace(review.Reason) ? "No search harness reason returned." : review.Reason.Trim();

        if (LooksBlockedByHardFilter(review, lead, filter))
        {
            decision = "ignore";
            reason = $"Hard filter conflict detected by search harness. {reason}";
            confidence = Math.Max(confidence, 0.75m);
        }
        else if (decision == "queue" && (fitScore < 60 || confidence < 0.60m))
        {
            decision = "review";
            reason = $"Queue decision downgraded for low fit/confidence. {reason}";
        }

        return review with
        {
            Decision = decision,
            FitScore = fitScore,
            Confidence = confidence,
            Reason = reason,
            Strengths = Clean(review.Strengths),
            Risks = Clean(review.Risks),
            DetectedSeniority = NormalizeOneOf(review.DetectedSeniority ?? "", ["intern", "junior", "mid", "senior", "lead", "unknown"]),
            DetectedWorkplace = NormalizeOneOf(review.DetectedWorkplace ?? "", ["remote", "hybrid", "onsite", "unknown"]),
            ApplyType = NormalizeOneOf(review.ApplyType ?? "", ["easy_apply", "external_form", "unknown"])
        };
    }

    public static JobFitReviewResponse UnavailableReview(string detail) =>
        new("review", 0, $"Search LLM review unavailable: {detail}", "unknown", "unknown", "unknown", [], ["llm_unavailable"], 0.2m);

    private static bool LooksBlockedByHardFilter(JobFitReviewResponse review, JobLead lead, JobFilter filter)
    {
        var haystack = $"{lead.Title} {lead.Description} {lead.Location} {review.DetectedSeniority} {review.DetectedWorkplace}".ToLowerInvariant();
        if ((filter.BlockedSeniorities ?? []).Any(x => ContainsTerm(haystack, x))) return true;
        if ((filter.BlockedRoleKeywords ?? []).Any(x => ContainsTerm(haystack, x))) return true;
        if ((filter.BlockedWorkplaceTypes ?? []).Any(x => ContainsTerm(haystack, x))) return true;

        var remoteOnly = (filter.AllowedWorkplaceTypes ?? []).Any(x => IsTerm(x, "remote") || IsTerm(x, "remoto")) &&
                         !(filter.AllowedWorkplaceTypes ?? []).Any(x => IsTerm(x, "hybrid") || IsTerm(x, "hibrido") || IsTerm(x, "onsite") || IsTerm(x, "presencial"));
        return remoteOnly && review.DetectedWorkplace is "onsite" or "hybrid";
    }

    private static string NormalizeDecision(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "queue" => "queue",
            "ignore" => "ignore",
            _ => "review"
        };

    private static string NormalizeOneOf(string value, string[] allowed)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : "unknown";
    }

    private static string[] Clean(string[] values) =>
        (values ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();

    private static bool ContainsTerm(string haystack, string term)
    {
        var normalized = RemoveDiacritics(term.ToLowerInvariant()).Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        var normalizedHaystack = RemoveDiacritics(haystack);
        if (normalized.Length <= 3 || normalized.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            var pattern = $@"(^|[^a-z0-9]){System.Text.RegularExpressions.Regex.Escape(normalized)}([^a-z0-9]|$)";
            return System.Text.RegularExpressions.Regex.IsMatch(normalizedHaystack, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return normalizedHaystack.Contains(normalized);
    }

    private static bool IsTerm(string value, string expected) =>
        RemoveDiacritics(value.ToLowerInvariant()).Equals(RemoveDiacritics(expected.ToLowerInvariant()), StringComparison.OrdinalIgnoreCase);

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
