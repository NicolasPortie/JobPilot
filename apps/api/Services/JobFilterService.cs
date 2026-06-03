using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public class JobFilterService(JobPilotDbContext db)
{
    public async Task<JobFilter> GetOrCreateAsync(Guid userId)
    {
        var filter = await db.JobFilters.FirstOrDefaultAsync(x => x.UserId == userId);
        if (filter is not null)
        {
            var normalized = NormalizeApplyTypes(filter.AllowedApplyTypes);
            if (!filter.AllowedApplyTypes.SequenceEqual(normalized))
            {
                filter.AllowedApplyTypes = normalized;
                filter.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }

            return filter;
        }

        filter = new JobFilter
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AllowedSeniorities = ["junior"],
            BlockedSeniorities = ["senior", "sênior", "sr", "sr.", "pleno", "pl", "staff", "principal", "lead", "manager", "coordenador"],
            AllowedRoleKeywords = ["asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect"],
            BlockedRoleKeywords = ["suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados"],
            AllowedWorkplaceTypes = ["remote"],
            BlockedWorkplaceTypes = ["hybrid", "híbrido", "hibrido", "onsite", "on-site", "presencial"],
            AllowedApplyTypes = ["linkedin_easy_apply", "inhire"],
            MinScore = 30,
            AutoIgnoreLowScore = true
        };
        db.JobFilters.Add(filter);
        await db.SaveChangesAsync();
        return filter;
    }

    public bool ShouldIgnore(JobLead lead, JobFilter filter, out string reason)
    {
        reason = "";
        var rawHaystack = $"{lead.Title} {lead.Description} {lead.Location}".ToLowerInvariant();
        var haystack = RemoveDiacritics(rawHaystack);

        if (IsDemoOrLocalLead(lead, haystack))
        {
            reason = "test_fixture: vaga local/demo nao deve entrar no autopilot";
            return true;
        }

        foreach (var blocked in filter.BlockedSeniorities)
        {
            if (ContainsTerm(haystack, blocked))
            {
                reason = $"seniority_mismatch: contem '{blocked}'";
                return true;
            }
        }

        var blockedWorkplaceTypes = filter.BlockedWorkplaceTypes ?? [];
        if (blockedWorkplaceTypes.Any(w => ContainsTerm(haystack, w)))
        {
            var matched = blockedWorkplaceTypes.First(w => ContainsTerm(haystack, w));
            reason = $"workplace_mismatch: contem '{matched}'";
            return true;
        }

        var blockedRoleKeywords = filter.BlockedRoleKeywords ?? [];
        if (blockedRoleKeywords.Any(w => ContainsTerm(haystack, w)))
        {
            var matched = blockedRoleKeywords.First(w => ContainsTerm(haystack, w));
            reason = $"role_mismatch: contem '{matched}'";
            return true;
        }

        var remoteOnly = filter.AllowedWorkplaceTypes.Any(w => IsTerm(w, "remote") || IsTerm(w, "remoto")) &&
                         !filter.AllowedWorkplaceTypes.Any(w => IsTerm(w, "hybrid") || IsTerm(w, "hibrido") || IsTerm(w, "onsite") || IsTerm(w, "presencial"));
        if (!lead.IsRemote && remoteOnly)
        {
            if (HasRemoteSignal(lead, haystack))
                lead.IsRemote = true;
            else
                lead.ScoreReason = AppendReason(lead.ScoreReason, "modelo de trabalho sem sinal remoto explicito; manter para revisao");
        }

        if (filter.AutoIgnoreLowScore && lead.Score < filter.MinScore)
        {
            reason = $"low_score: {lead.Score} < {filter.MinScore}";
            return true;
        }

        return false;
    }

    public string BuildSearchUrl(JobFilter filter, string keywords, string location, int limit)
    {
        var query = Uri.EscapeDataString(keywords);
        var loc = Uri.EscapeDataString(location);
        var url = $"https://www.linkedin.com/jobs/search/?keywords={query}&location={loc}&f_TPR=r604800";

        // Keep LinkedIn discovery broad. LinkedIn often omits junior/remote
        // facets for jobs that still match after reading the posting.

        if (!filter.AllowedApplyTypes.Contains("external_form", StringComparer.OrdinalIgnoreCase) &&
            !filter.AllowedApplyTypes.Contains("inhire", StringComparer.OrdinalIgnoreCase) &&
            !filter.AllowedApplyTypes.Contains("unsupported_external", StringComparer.OrdinalIgnoreCase))
        {
            url += "&f_AL=true";
        }

        return url;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static bool ContainsAnyTerm(string normalizedHaystack, IEnumerable<string> terms) =>
        terms.Any(term => ContainsTerm(normalizedHaystack, term));

    private static bool HasRemoteSignal(JobLead lead, string normalizedHaystack)
    {
        if (ContainsAnyTerm(normalizedHaystack, ["remote", "remoto", "home office", "work from home", "anywhere"])) return true;

        var location = RemoveDiacritics((lead.Location ?? "").ToLowerInvariant()).Trim();
        return location is "remote search filter";
    }

    private static bool ContainsTerm(string normalizedHaystack, string term)
    {
        var normalizedTerm = RemoveDiacritics(term.ToLowerInvariant()).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;
        if (normalizedTerm.Length <= 3 || normalizedTerm.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            var pattern = $@"(^|[^a-z0-9]){System.Text.RegularExpressions.Regex.Escape(normalizedTerm)}([^a-z0-9]|$)";
            return System.Text.RegularExpressions.Regex.IsMatch(normalizedHaystack, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return normalizedHaystack.Contains(normalizedTerm);
    }

    private static bool IsTerm(string value, string expected) =>
        RemoveDiacritics(value.ToLowerInvariant()).Equals(RemoveDiacritics(expected.ToLowerInvariant()), StringComparison.OrdinalIgnoreCase);

    private static string AppendReason(string current, string reason) =>
        string.IsNullOrWhiteSpace(current)
            ? reason
            : current.Contains(reason, StringComparison.OrdinalIgnoreCase) ? current : $"{current}; {reason}";

    private static bool IsDemoOrLocalLead(JobLead lead, string normalizedHaystack)
    {
        if (Uri.TryCreate(lead.Url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host is "localhost" or "127.0.0.1" or "::1") return true;
            if (uri.AbsoluteUri.Contains("/jobs/view/1234567890", StringComparison.OrdinalIgnoreCase)) return true;
        }

        var company = RemoveDiacritics((lead.Company ?? "").ToLowerInvariant());
        return normalizedHaystack.Contains("fake ") ||
               company.Contains("fixture") ||
               company.Contains("acme tech");
    }

    private static string[] NormalizeApplyTypes(string[] values)
    {
        var normalized = values
            .SelectMany(value => value.ToLowerInvariant() switch
            {
                "easy_apply" => ["linkedin_easy_apply"],
                "external_form" => ["inhire"],
                "email" => Array.Empty<string>(),
                _ => [value]
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? ["linkedin_easy_apply", "inhire"] : normalized;
    }
}
