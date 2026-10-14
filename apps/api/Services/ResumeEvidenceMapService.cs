using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeEvidenceMapService(
    IRepository<ResumeVersion> versions,
    IRepository<JobTarget> targets,
    IRepository<CandidateProfile> profiles,
    IRepository<ResumeEvidenceMapRun> mapRuns,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[][] AdjacentGroups =
    [
        ["c#", "csharp", ".net", "dotnet", "asp.net", "asp.net-core", "entity-framework", "ef-core"],
        ["javascript", "typescript", "react", "html", "html5", "css", "css3", "frontend"],
        ["api", "apis", "api-rest", "apis-rest", "rest", "backend", "integracoes", "integrações"],
        ["sql", "sql-server", "postgresql", "postgres", "mysql", "firebird", "mongodb", "banco-de-dados", "database"],
        ["qa", "testes", "testing", "selenium", "playwright", "automacao-de-testes", "automação-de-testes"],
        ["docker", "containers", "kubernetes", "k8s", "devops", "azure-devops", "ci-cd", "github-actions"],
        ["rabbitmq", "mensageria", "filas", "integracoes", "backend", "apis", "api-rest"],
        ["orientacao-a-objetos", "oop", "injecao-de-dependencia", "dependency-injection", "c#", "csharp", ".net", "dotnet", "asp.net-core"]
    ];

    // Grafias equivalentes: citar qualquer uma delas no currículo comprova o requisito.
    private static readonly string[][] Aliases =
    [
        ["entity-framework", "entity-framework-core", "ef-core", "efcore"],
        ["api-rest", "apis-rest", "web-apis-rest", "restful", "rest-api", "rest-apis"],
        ["postgresql", "postgres"],
        ["javascript", "js"],
        ["typescript", "ts"],
        ["kubernetes", "k8s"],
        ["node.js", "nodejs", "node"],
        ["ci-cd", "cicd"],
        ["testes", "testes-unitarios", "unit-testing", "testes-de-unidade", "unit-tests"],
        ["sql-server", "mssql", "microsoft-sql-server"],
        ["asp.net-core", "aspnet-core"],
        ["github-actions", "gh-actions"],
        ["ingles", "english"]
    ];

    public async Task<ResumeEvidenceMapDetail> BuildAsync(Guid userId, BuildResumeEvidenceMapRequest request)
    {
        var version = await versions.Query()
            .Include(x => x.Sections)
            .FirstOrDefaultAsync(x => x.Id == request.ResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");

        var target = await targets.Query()
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == request.JobTargetId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga alvo não encontrada.");

        var profile = await profiles.FirstOrDefaultAsync(x => x.UserId == userId);
        var corpus = BuildCorpus(version, profile);
        var run = new ResumeEvidenceMapRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = version.Id,
            JobTargetId = target.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var items = target.Requirements
            .OrderBy(x => x.SortOrder)
            .Select((requirement, index) => Evaluate(userId, run.Id, requirement, corpus, index + 1))
            .ToList();
        run.Items.AddRange(items);

        var score = ResumeEvidenceScoreCalculator.Calculate(items);
        run.EvidenceScore = score.Score;
        run.Confidence = score.Confidence;
        run.Status = score.Status;
        run.WarningsJson = JsonSerializer.Serialize(score.Warnings, JsonOptions);

        mapRuns.Add(run);
        await unitOfWork.SaveChangesAsync();
        return ToDetail(run);
    }

    public async Task<ResumeEvidenceMapDetail?> GetAsync(Guid userId, Guid id)
    {
        var run = await mapRuns.Query()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        return run is null ? null : ToDetail(run);
    }

    private static ResumeRequirementEvidence Evaluate(Guid userId, Guid runId, JobRequirement requirement, EvidenceCorpus corpus, int order)
    {
        var terms = RequirementTerms(requirement);
        foreach (var term in terms)
        {
            if (TryFindEvidence(corpus, term, out var evidence))
            {
                return BuildItem(userId, runId, requirement, order, "proven", evidence.Text, evidence.Source, $"Termo encontrado: {term}", 0.9m);
            }
        }

        if (TryFindAdjacent(corpus, terms, out var adjacent))
        {
            return BuildItem(userId, runId, requirement, order, "adjacent", adjacent.Text, adjacent.Source, adjacent.Reason, 0.65m);
        }

        var status = requirement.Category is "education" or "language" or "certification" or "domain" ? "unknown" : "missing";
        var reason = status == "unknown"
            ? "Requisito precisa de confirmação do usuário antes de entrar no currículo."
            : "Nenhuma evidência encontrada no perfil ou currículo base.";
        return BuildItem(userId, runId, requirement, order, status, "", "", reason, status == "unknown" ? 0.3m : 0.15m);
    }

    private static ResumeRequirementEvidence BuildItem(Guid userId, Guid runId, JobRequirement requirement, int order, string status, string evidence, string source, string reason, decimal confidence) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeEvidenceMapRunId = runId,
            JobRequirementId = requirement.Id,
            RequirementLabel = requirement.Label,
            RequirementCategory = requirement.Category,
            RequirementImportance = requirement.Importance,
            MatchStatus = status,
            EvidenceText = evidence,
            EvidenceSource = source,
            Reason = reason,
            Confidence = confidence,
            SortOrder = order
        };

    private static EvidenceCorpus BuildCorpus(ResumeVersion version, CandidateProfile? profile)
    {
        var entries = new List<EvidenceEntry>();
        if (profile is not null)
        {
            Add(entries, "profile.skills", string.Join(", ", profile.Skills));
            Add(entries, "profile.summary", profile.ProfessionalSummary);
            // O cargo desejado é intenção, não experiência: não entra como evidência.
        }

        Add(entries, "resume.plain_text", version.PlainText);
        foreach (var section in version.Sections)
            Add(entries, $"resume.section.{section.SectionType}", section.ContentJson);

        var combined = string.Join("\n", entries.Select(x => x.Text));
        return new EvidenceCorpus(entries, Normalize(combined));
    }

    private static void Add(List<EvidenceEntry> entries, string source, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            entries.Add(new EvidenceEntry(source, text.Trim(), Normalize(text)));
    }

    private static string[] RequirementTerms(JobRequirement requirement)
    {
        var terms = (requirement.ExactPhrases ?? [])
            .Concat([requirement.Label, requirement.NormalizedKeyword])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeKeyword)
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var group in Aliases.Where(group => terms.Any(term => group.Contains(term, StringComparer.OrdinalIgnoreCase))))
            terms.AddRange(group.Where(alias => !terms.Contains(alias, StringComparer.OrdinalIgnoreCase)));
        return terms.ToArray();
    }

    private static bool TryFindEvidence(EvidenceCorpus corpus, string term, out EvidenceHit evidence)
    {
        foreach (var entry in corpus.Entries)
        {
            if (ContainsTerm(entry.Normalized, term))
            {
                evidence = new EvidenceHit(Snippet(entry.Text, term), entry.Source, "");
                return true;
            }
        }

        evidence = default;
        return false;
    }

    private static bool TryFindAdjacent(EvidenceCorpus corpus, string[] terms, out EvidenceHit evidence)
    {
        foreach (var term in terms)
        {
            var group = AdjacentGroups.FirstOrDefault(group => group.Any(item => SameKeyword(item, term)));
            if (group is null) continue;

            foreach (var adjacent in group.Where(item => !SameKeyword(item, term)))
            {
                if (TryFindEvidence(corpus, adjacent, out var hit))
                {
                    evidence = hit with { Reason = $"Requisito '{term}' tem evidência adjacente via '{adjacent}'." };
                    return true;
                }
            }
        }

        evidence = default;
        return false;
    }

    private static ResumeEvidenceMapDetail ToDetail(ResumeEvidenceMapRun run)
    {
        var warnings = ParseStringArray(run.WarningsJson);
        return new ResumeEvidenceMapDetail(
            run.Id,
            run.ResumeVersionId,
            run.JobTargetId,
            run.Status,
            run.EvidenceScore,
            run.Confidence,
            warnings,
            run.Items
                .OrderBy(x => x.SortOrder)
                .Select(x => new ResumeRequirementEvidenceDto(x.Id, x.JobRequirementId, x.RequirementLabel, x.RequirementCategory, x.RequirementImportance, x.MatchStatus, x.EvidenceText, x.EvidenceSource, x.Reason, x.Confidence, x.SortOrder))
                .ToArray(),
            run.CreatedAt);
    }

    private static string[] ParseStringArray(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static string Snippet(string text, string term)
    {
        var normalized = Normalize(text);
        var index = normalized.IndexOf(NormalizeKeyword(term), StringComparison.Ordinal);
        if (index < 0) return text[..Math.Min(text.Length, 220)];
        var start = Math.Max(0, index - 100);
        var end = Math.Min(text.Length, index + term.Length + 140);
        return text[start..end].Trim();
    }

    private static bool ContainsTerm(string normalizedHaystack, string term)
    {
        // Sempre com fronteira de token: substring pura gera falso "proven"
        // (ex.: "java" dentro de "javascript"). Mesmo critério dos ATS reais.
        var normalizedTerm = NormalizeKeyword(term);
        if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;
        var escaped = Regex.Escape(normalizedTerm)
            .Replace("\\-", "[^a-z0-9]+")
            .Replace("-", "[^a-z0-9]+");
        var pattern = $@"(^|[^a-z0-9]){escaped}([^a-z0-9]|$)";
        return Regex.IsMatch(normalizedHaystack, pattern, RegexOptions.IgnoreCase);
    }

    private static bool SameKeyword(string left, string right) => NormalizeKeyword(left) == NormalizeKeyword(right);

    private static string NormalizeKeyword(string value)
    {
        var normalized = Normalize(value);
        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.]+", "-").Trim('-');
        return normalized;
    }

    private static string Normalize(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return withoutDiacritics.ToLowerInvariant();
    }

    private sealed record EvidenceCorpus(List<EvidenceEntry> Entries, string Normalized);
    private sealed record EvidenceEntry(string Source, string Text, string Normalized);
    private readonly record struct EvidenceHit(string Text, string Source, string Reason);
}
