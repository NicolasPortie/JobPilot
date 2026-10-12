using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class DiscoveredJobMapper
{
    public static bool IsProtectedStatus(string status) =>
        status is JobStatuses.Applied
            or JobStatuses.QueuedToApply
            or JobStatuses.Opening
            or JobStatuses.Filling
            or JobStatuses.ReadyForSubmit
            or JobStatuses.ManualOnly
            or JobStatuses.NeedsReview;

    public static bool IsInvalid(DiscoveredJobDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Url))
            return true;

        var normalizedTitle = RemoveDiacritics(item.Title).ToLowerInvariant();
        return normalizedTitle.Contains("volte a experimentar o premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("experimente o premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("try premium", StringComparison.Ordinal) ||
               normalizedTitle.Contains("premium gratis", StringComparison.Ordinal) ||
               normalizedTitle.Contains("premium free", StringComparison.Ordinal);
    }

    public static void ApplyReview(JobLead lead, JobFitReviewResponse review)
    {
        var reason = $"search_harness: {review.Reason}";
        var details = BuildReviewDetails(review);
        lead.Score = review.FitScore;
        lead.ScoreReason = string.IsNullOrWhiteSpace(details) ? reason : $"{reason}; {details}";

        (lead.Status, lead.FilteredReason) = review.Decision switch
        {
            "ignore" => (JobStatuses.Ignored, reason),
            "review" => (JobStatuses.NeedsReview, reason),
            _ => (JobStatuses.Scored, "")
        };
    }

    private static string BuildReviewDetails(JobFitReviewResponse review)
    {
        var parts = new List<string>
        {
            $"fit={review.FitScore}",
            $"confidence={review.Confidence:0.00}",
            $"seniority={review.DetectedSeniority}",
            $"workplace={review.DetectedWorkplace}",
            $"apply={review.ApplyType}"
        };
        if (review.Strengths.Length > 0) parts.Add("strengths=" + string.Join(", ", review.Strengths));
        if (review.Risks.Length > 0) parts.Add("risks=" + string.Join(", ", review.Risks));
        return string.Join("; ", parts);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
