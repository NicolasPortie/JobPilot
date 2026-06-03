using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public class ScoringService
{
    private static readonly string[] NegativeTerms = ["senior", "sênior", "sr", "sr.", "pleno", "staff", "principal", "lead", "manager", "coordenador"];
    private static readonly string[] RemoteTerms = ["remote", "remoto", "home office", "work from home", "anywhere"];

    public void Score(JobLead lead, CandidateProfile? profile, JobFilter? filter = null)
    {
        var score = 20;
        var reasons = new List<string>();
        var haystack = RemoveDiacritics($"{lead.Title} {lead.Description} {lead.Location}".ToLowerInvariant());

        if (profile is not null)
        {
            var targetTerms = profile.TargetRole.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var roleHits = targetTerms.Count(term => haystack.Contains(RemoveDiacritics(term.ToLowerInvariant())));
            if (roleHits > 0)
            {
                score += Math.Min(25, roleHits * 8);
                reasons.Add("cargo alvo aparece na vaga");
            }

            if (ContainsAny(haystack, ["developer", "desenvolvedor", "desenvolvedora", "programador", "programadora", "fullstack", "full stack", "full-stack"]))
            {
                score += 8;
                reasons.Add("cargo tecnico compativel");
            }

            var skillTerms = ExpandSkillTerms(profile.Skills);
            var skillHits = skillTerms.Count(skill => haystack.Contains(skill));
            if (skillHits > 0)
            {
                score += Math.Min(25, skillHits * 6);
                reasons.Add($"{skillHits} skill(s) detectadas");
            }
            else if (profile.Skills.Count() > 0)
            {
                score -= 18;
                reasons.Add("nenhuma skill alvo detectada");
            }

            if (!string.IsNullOrWhiteSpace(profile.Seniority) && haystack.Contains(RemoveDiacritics(profile.Seniority.ToLowerInvariant())))
            {
                score += 10;
                reasons.Add("senioridade compativel");
            }
        }

        if (RemoteTerms.Any(haystack.Contains) || lead.IsRemote)
        {
            score += 10;
            lead.IsRemote = true;
            reasons.Add("sinal de remoto");
        }

        if (NegativeTerms.Any(term => haystack.Contains(term)))
        {
            score -= 25;
            reasons.Add("possivel senioridade acima do alvo");
        }

        if (filter is not null)
        {
            var allowedRoleHits = (filter.AllowedRoleKeywords ?? []).Count(role => ContainsTerm(haystack, role));
            if (allowedRoleHits > 0)
            {
                score += Math.Min(18, allowedRoleHits * 6);
                reasons.Add($"{allowedRoleHits} termo(s) de cargo permitido");
            }

            foreach (var blocked in filter.BlockedSeniorities ?? [])
            {
                if (ContainsTerm(haystack, blocked))
                {
                    score -= 100;
                    reasons.Add($"BLOQUEADO: senioridade '{blocked}'");
                    break;
                }
            }

            var blockedWorkplaceTypes = filter.BlockedWorkplaceTypes ?? [];
            if (blockedWorkplaceTypes.Any(w => ContainsTerm(haystack, w)))
            {
                score -= 100;
                var matched = blockedWorkplaceTypes.First(w => ContainsTerm(haystack, w));
                reasons.Add($"BLOQUEADO: modelo '{matched}'");
            }

            var blockedRoleKeywords = filter.BlockedRoleKeywords ?? [];
            if (blockedRoleKeywords.Any(w => ContainsTerm(haystack, w)))
            {
                score -= 100;
                var matched = blockedRoleKeywords.First(w => ContainsTerm(haystack, w));
                reasons.Add($"BLOQUEADO: cargo '{matched}'");
            }
        }

        if (string.IsNullOrWhiteSpace(lead.Description))
        {
            score -= 10;
            reasons.Add("descricao vazia");
        }

        lead.Score = Math.Clamp(score, 0, 100);
        lead.Status = JobStatuses.Scored;
        lead.ScoreReason = reasons.Count == 0 ? "score inicial por dados basicos" : string.Join("; ", reasons);
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

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static List<string> ExpandSkillTerms(IEnumerable<string> skills)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills)
        {
            var normalized = RemoveDiacritics(skill.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(normalized)) terms.Add(normalized);
            if (normalized.Contains("asp.net") || normalized.Contains("c#") || normalized.Contains("csharp"))
            {
                terms.Add(".net");
                terms.Add("dotnet");
                terms.Add("net core");
            }
            if (normalized.Contains("entity framework"))
            {
                terms.Add("ef core");
            }
        }

        return terms.ToList();
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(value.Contains);
}
