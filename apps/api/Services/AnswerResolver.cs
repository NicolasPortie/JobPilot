using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public class AnswerResolver(JobPilotDbContext db)
{
    public async Task<ResolvedAnswer> ResolveAsync(Guid userId, string questionText, string fieldContext, CandidateProfile? profile)
    {
        var patterns = await db.QuestionPatterns
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UseCount)
            .ToListAsync();

        if (patterns.Count == 0)
        {
            return new ResolvedAnswer("", "no_patterns", 0m, "Nenhum pattern cadastrado.");
        }

        var normalizedQuestion = Normalize(questionText);
        var bestMatch = patterns
            .Select(p => new { Pattern = p, Score = MatchScore(normalizedQuestion, p) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch is null || bestMatch.Score < 0.45m)
        {
            return new ResolvedAnswer("", "checkpoint", bestMatch?.Score ?? 0m, $"Nenhum pattern com match confiavel. Melhor match: {bestMatch?.Pattern.PatternKey} com {bestMatch?.Score:0.00}");
        }

        var pattern = bestMatch.Pattern;
        var confidence = bestMatch.Score;

        if (pattern.ValueSource == "checkpoint")
        {
            return new ResolvedAnswer("", "checkpoint", confidence, $"Pattern '{pattern.PatternKey}' manda abrir checkpoint para esta pergunta.");
        }

        if (pattern.ValueSource == "profile_field" && profile is not null)
        {
            var answer = FillTemplate(pattern.AnswerTemplate, profile, pattern.ProfileFieldMapping);
            if (!string.IsNullOrWhiteSpace(answer) && !answer.Contains("{{"))
            {
                pattern.UseCount++;
                pattern.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                return new ResolvedAnswer(answer, "profile_template", confidence, $"Pattern '{pattern.PatternKey}' preenchido com dados do perfil.");
            }
        }

        if (pattern.ValueSource == "reusable")
        {
            var answer = FillTemplate(pattern.AnswerTemplate, profile, "");
            if (!string.IsNullOrWhiteSpace(answer) && !answer.Contains("{{"))
            {
                pattern.UseCount++;
                pattern.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                return new ResolvedAnswer(answer, "reusable_template", confidence, $"Pattern '{pattern.PatternKey}' com template fixo.");
            }
        }

        if (pattern.ValueSource == "ai_draft")
        {
            var template = FillTemplate(pattern.AnswerTemplate, profile, pattern.ProfileFieldMapping);
            return new ResolvedAnswer(template, "ai_draft", confidence * 0.9m, $"Pattern '{pattern.PatternKey}' sugere template para IA completar.");
        }

        return new ResolvedAnswer("", "checkpoint", confidence, $"Pattern '{pattern.PatternKey}' encontrado mas sem dados suficientes.");
    }

    private static decimal MatchScore(string question, QuestionPattern pattern)
    {
        var scores = new List<decimal>();
        var q = question;

        foreach (var template in pattern.QuestionTemplates)
        {
            var t = Normalize(template);
            var sim = FuzzySimilarity(q, t);
            scores.Add(sim);
        }

        var keywordScore = pattern.Tags.Sum(tag => q.Contains(Normalize(tag)) ? 0.15m : 0m);
        var bestTemplateScore = scores.Count > 0 ? scores.Max() : 0m;

        var total = Math.Min(1.0m, bestTemplateScore + keywordScore);
        return total;
    }

    private static decimal FuzzySimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0m;
        if (a == b) return 1.0m;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0m;
        return 1.0m - ((decimal)distance / maxLen);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return Regex.Replace(sb.ToString().ToLowerInvariant(), @"[^a-z0-9]", " ");
    }

    private static string FillTemplate(string template, CandidateProfile? profile, string fieldMapping)
    {
        if (profile is null) return template;
        var result = template;

        result = result.Replace("{{profile.FullName}}", profile.FullName, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Cpf}}", profile.Cpf, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Email}}", profile.Email, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Phone}}", profile.Phone, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.City}}", profile.City, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Country}}", profile.Country, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.LinkedinUrl}}", profile.LinkedinUrl, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.GithubUrl}}", profile.GithubUrl, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.PortfolioUrl}}", profile.PortfolioUrl, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.ProfessionalSummary}}", profile.ProfessionalSummary, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.TargetRole}}", profile.TargetRole, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Seniority}}", profile.Seniority, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.SalaryExpectation}}", profile.SalaryExpectation, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.Skills}}", string.Join(", ", profile.Skills), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{{profile.ResumePath}}", profile.ResumePath, StringComparison.OrdinalIgnoreCase);

        return result;
    }
}
