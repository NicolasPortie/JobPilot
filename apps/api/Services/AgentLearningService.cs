using System.Text.Json;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class AgentLearningService(
    IRepository<AgentLearning> learnings,
    IRepository<Checkpoint> checkpoints,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SensitiveSignals =
    [
        "cpf", "rg", "documento", "visa", "visto", "autorizacao", "autorização",
        "deficiencia", "deficiência", "pcd", "raca", "raça", "genero", "gênero",
        "religiao", "religião", "passaporte", "passport", "disability", "race", "gender"
    ];

    public async Task<List<AgentLearning>> ListAsync(Guid userId, string? status, string? type = null, string? risk = null, string? domain = null)
    {
        var query = learnings.Query().Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(x => x.LearningType == type);
        if (!string.IsNullOrWhiteSpace(risk))
            query = query.Where(x => x.RiskLevel == risk);
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var normalizedDomain = NormalizeDomain(domain);
            query = query.Where(x => x.Domain == normalizedDomain);
        }
        return await query
            .OrderBy(x => x.Status == AgentLearningStatuses.Candidate ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
    }

    public async Task<AgentLearning?> GetAsync(Guid userId, Guid id) =>
        await learnings.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);

    public async Task<List<AgentLearning>> ActiveAsync(Guid userId) =>
        await learnings.Query()
            .Where(x => x.UserId == userId && (x.Status == AgentLearningStatuses.Approved || x.Status == AgentLearningStatuses.AutoApproved))
            .OrderByDescending(x => x.SuccessCount)
            .ThenBy(x => x.FailureCount)
            .Take(100)
            .ToListAsync();

    public async Task<AgentLearning> UpsertCandidateAsync(Guid userId, AgentLearningRequest request)
    {
        var classification = Classify(request);
        var domain = NormalizeDomain(request.Domain);
        var patternKey = StablePatternKey(request.Domain, request.PatternKey, classification.Type);
        var existing = await learnings.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.Domain == domain &&
            x.PatternKey == patternKey);

        if (existing is null)
        {
            existing = new AgentLearning
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationJobId = request.ApplicationJobId,
                Domain = domain,
                PatternKey = patternKey,
                CreatedAt = DateTimeOffset.UtcNow
            };
            learnings.Add(existing);
        }

        existing.LearningType = classification.Type;
        existing.RiskLevel = classification.Risk;
        existing.Title = string.IsNullOrWhiteSpace(request.Title) ? BuildTitle(classification.Type, patternKey) : request.Title.Trim();
        existing.Summary = request.Summary.Trim();
        existing.TriggerJson = SafeJson(request.TriggerJson);
        existing.ActionJson = SafeJson(request.ActionJson);
        existing.EvidenceJson = SafeJson(request.EvidenceJson);

        if (existing.Status is AgentLearningStatuses.Rejected or AgentLearningStatuses.Disabled)
        {
            await unitOfWork.SaveChangesAsync();
            return existing;
        }

        existing.Status = ShouldAutoApprove(existing)
            ? AgentLearningStatuses.AutoApproved
            : AgentLearningStatuses.Candidate;

        await unitOfWork.SaveChangesAsync();
        return existing;
    }

    public async Task<List<AgentLearning>> GenerateFromJobAsync(Guid userId, Guid applicationJobId)
    {
        var created = new List<AgentLearning>();
        var jobCheckpoints = await checkpoints.Query()
            .Where(x => x.UserId == userId && x.ApplicationJobId == applicationJobId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        foreach (var checkpoint in jobCheckpoints)
        {
            var request = BuildFromCheckpoint(checkpoint);
            if (request is null) continue;
            created.Add(await UpsertCandidateAsync(userId, request));
        }

        return created;
    }

    public async Task<AgentLearning?> ReviewAsync(Guid userId, Guid reviewerId, Guid id, string action)
    {
        var learning = await GetAsync(userId, id);
        if (learning is null) return null;

        learning.Status = action switch
        {
            "approve" => AgentLearningStatuses.Approved,
            "reject" => AgentLearningStatuses.Rejected,
            "disable" => AgentLearningStatuses.Disabled,
            _ => learning.Status
        };
        learning.ReviewedAt = DateTimeOffset.UtcNow;
        learning.ReviewedBy = reviewerId;
        await unitOfWork.SaveChangesAsync();
        return learning;
    }

    public async Task<AgentLearning?> RecordOutcomeAsync(Guid userId, AgentLearningOutcomeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PatternKey))
            return null;

        var patternKey = request.PatternKey.Trim();
        var learning = await learnings.FirstOrDefaultAsync(x => x.UserId == userId && x.PatternKey == patternKey);
        if (learning is null) return null;

        if (request.Success)
        {
            learning.SuccessCount++;
            if (ShouldAutoApprove(learning))
            {
                learning.Status = AgentLearningStatuses.AutoApproved;
                learning.ReviewedAt ??= DateTimeOffset.UtcNow;
            }
        }
        else
        {
            learning.FailureCount++;
            if (learning.FailureCount >= 2 && learning.IsActive)
                learning.Status = AgentLearningStatuses.Disabled;
        }

        await unitOfWork.SaveChangesAsync();
        return learning;
    }

    public static bool ShouldAutoApprove(AgentLearning learning) =>
        learning.RiskLevel == AgentLearningRiskLevels.Low &&
        learning.SuccessCount >= 2 &&
        learning.FailureCount == 0 &&
        learning.LearningType is AgentLearningTypes.Technical or AgentLearningTypes.Format &&
        HasSpecificPattern(learning) &&
        !TouchesSensitiveField($"{learning.Title} {learning.Summary} {learning.TriggerJson} {learning.ActionJson}");

    private static bool HasSpecificPattern(AgentLearning learning)
    {
        var text = RemoveDiacritics($"{learning.PatternKey} {learning.Title} {learning.Summary}").ToLowerInvariant();
        var hasAllowedSpecificSignal =
            text.Contains("combobox", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("dropdown", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("salary", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("salario", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("format", StringComparison.OrdinalIgnoreCase);

        var isGenericClick =
            text.Contains("click", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("clique", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("botao", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("button", StringComparison.OrdinalIgnoreCase);

        if (isGenericClick && !hasAllowedSpecificSignal)
            return false;

        var tokens = text
            .Split(['-', '_', ':', ' ', '.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 5)
            .Where(token => token is not ("click" or "clique" or "botao" or "button" or "submit" or "enviar" or "avancar" or "revisar" or "review" or "linkedin" or "technical" or "tecnica" or "formato" or "generico" or "generic" or "validado"))
            .ToArray();

        return tokens.Length > 0 || hasAllowedSpecificSignal;
    }

    private static AgentLearningRequest? BuildFromCheckpoint(Checkpoint checkpoint)
    {
        var text = $"{checkpoint.ReasonCode} {checkpoint.Stage} {checkpoint.FieldLabel} {checkpoint.Detail}";
        var type = AgentLearningTypes.Technical;
        var risk = AgentLearningRiskLevels.Low;
        var patternKey = checkpoint.ReasonCode;
        var title = "Aprendizado técnico candidato";
        var summary = checkpoint.Detail;

        if (TouchesSensitiveField(text))
        {
            type = AgentLearningTypes.Sensitive;
            risk = AgentLearningRiskLevels.High;
            title = "Pergunta sensível exige revisão";
        }
        else if (checkpoint.ReasonCode.Contains("validation", StringComparison.OrdinalIgnoreCase))
        {
            type = AgentLearningTypes.Format;
            patternKey = "format_validation";
            title = "Formato de campo rejeitado pelo formulário";
        }
        else if (checkpoint.Detail.Contains("post-apply", StringComparison.OrdinalIgnoreCase) ||
                 checkpoint.Detail.Contains("foi enviada", StringComparison.OrdinalIgnoreCase))
        {
            type = AgentLearningTypes.Technical;
            patternKey = "confirmation_detection";
            title = "Reconhecer confirmação pós-candidatura";
        }
        else if (checkpoint.ReasonCode.Contains("stuck", StringComparison.OrdinalIgnoreCase))
        {
            type = AgentLearningTypes.Workflow;
            risk = AgentLearningRiskLevels.Medium;
            patternKey = "workflow_stuck";
            title = "Fluxo ficou preso";
        }

        return new AgentLearningRequest(
            checkpoint.ApplicationJobId,
            ExtractDomain(checkpoint.Detail),
            type,
            patternKey,
            title,
            summary,
            JsonSerializer.Serialize(new { checkpoint.ReasonCode, checkpoint.Stage, checkpoint.FieldLabel }, JsonOptions),
            JsonSerializer.Serialize(new { suggestion = title }, JsonOptions),
            JsonSerializer.Serialize(new { checkpoint.Id, checkpoint.CreatedAt, checkpoint.Detail }, JsonOptions),
            risk);
    }

    private static (string Type, string Risk) Classify(AgentLearningRequest request)
    {
        var text = $"{request.LearningType} {request.Title} {request.Summary} {request.TriggerJson} {request.ActionJson}";
        if (TouchesSensitiveField(text))
            return (AgentLearningTypes.Sensitive, AgentLearningRiskLevels.High);

        if (request.LearningType is AgentLearningTypes.Answer)
            return (AgentLearningTypes.Answer, AgentLearningRiskLevels.Medium);

        if (request.LearningType is AgentLearningTypes.Format)
            return (AgentLearningTypes.Format, request.RiskLevel == AgentLearningRiskLevels.Low ? AgentLearningRiskLevels.Low : AgentLearningRiskLevels.Medium);

        if (request.LearningType is AgentLearningTypes.Technical)
            return (AgentLearningTypes.Technical, AgentLearningRiskLevels.Low);

        return (request.LearningType, string.IsNullOrWhiteSpace(request.RiskLevel) ? AgentLearningRiskLevels.Medium : request.RiskLevel);
    }

    private static bool TouchesSensitiveField(string text)
    {
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        return SensitiveSignals.Any(signal =>
        {
            var normalizedSignal = RemoveDiacritics(signal).ToLowerInvariant();
            return System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                $@"(^|[^a-z0-9]){System.Text.RegularExpressions.Regex.Escape(normalizedSignal)}([^a-z0-9]|$)");
        });
    }

    private static string StablePatternKey(string domain, string patternKey, string type)
    {
        var raw = $"{NormalizeDomain(domain)}:{type}:{patternKey}".ToLowerInvariant();
        var chars = raw.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var compact = System.Text.RegularExpressions.Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return compact[..Math.Min(compact.Length, 120)];
    }

    private static string BuildTitle(string type, string key) =>
        type switch
        {
            AgentLearningTypes.Technical => $"Regra técnica: {key}",
            AgentLearningTypes.Format => $"Formato validável: {key}",
            AgentLearningTypes.Answer => $"Resposta candidata: {key}",
            AgentLearningTypes.Sensitive => $"Revisão sensível: {key}",
            _ => $"Aprendizado: {key}"
        };

    private static string SafeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch
        {
            return JsonSerializer.Serialize(new { text = json }, JsonOptions);
        }
    }

    private static string ExtractDomain(string detail)
    {
        var match = System.Text.RegularExpressions.Regex.Match(detail, @"url=(https?://[^/;\s]+)");
        if (!match.Success) return "";
        return NormalizeDomain(match.Groups[1].Value);
    }

    private static string NormalizeDomain(string value)
    {
        var host = Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.Host
            : value ?? "";
        host = host.Trim().ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
    }
}
