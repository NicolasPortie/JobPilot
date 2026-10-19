using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Resolvedor semântico de campos de formulário.
/// Combina: field type inference, profile mapping, session awareness e reusable answers.
/// Substitui o approach keyword-matching puro por inferência multi-sinal.
/// </summary>
public sealed class SemanticFieldResolver
{
    private readonly FieldTypeClassifier _classifier = new();
    private readonly SemanticProfileMapper _mapper = new();

    /// <summary>
    /// Resolve o valor para um campo, considerando todos os sinais disponíveis.
    /// Prioridade: reusable answer → semantic profile mapping → patterns → fallback.
    /// </summary>
    public FieldResolution Resolve(ElementCandidate field, CandidateProfile profile, SessionContext ctx, List<ReusableAnswer> answers, List<QuestionPattern> patterns, string jobContext)
    {
        var reusable = TryReusableAnswer(field, answers);
        if (reusable is not null)
            return new FieldResolution(reusable, SemanticFieldType.FreeText, 0.95m, "reusable-answer", true);

        var semanticValue = _mapper.Map(field, profile, ctx, answers);
        if (semanticValue is not null)
        {
            var inferredType = _classifier.Infer(field);
            var confidence = _classifier.Confidence(field, inferredType);
            return new FieldResolution(semanticValue, inferredType, confidence,
                $"semantic-inference:{inferredType}:conf={confidence:0.00}", confidence >= 0.5m);
        }

        var patternValue = TryPattern(field, patterns, profile, answers);
        if (patternValue is not null)
            return new FieldResolution(patternValue, SemanticFieldType.Unknown, 0.6m, "question-pattern", true);

        var keywordValue = FormFiller.ResolveValue(field.Label, profile, answers);
        if (!string.IsNullOrWhiteSpace(keywordValue))
            return new FieldResolution(keywordValue, SemanticFieldType.Unknown, 0.4m, "keyword-fallback", false);

        return new FieldResolution(null, SemanticFieldType.Unknown, 0m, "no-match", false);
    }

    /// <summary>
    /// Tenta resolver uma alternativa quando o valor principal falhou validação ou foi rejeitado.
    /// </summary>
    public FieldResolution? ResolveAlternative(ElementCandidate field, CandidateProfile profile, SessionContext ctx, List<ReusableAnswer> answers, List<QuestionPattern> patterns, int attempt)
    {
        var inferredType = _classifier.Infer(field);

        return attempt switch
        {
            // Tentativa 1: tentar formato diferente do mesmo campo
            1 when inferredType == SemanticFieldType.Phone =>
                TryAlternativeFormat(field, profile.Phone, SemanticFieldType.Phone),
            1 when inferredType == SemanticFieldType.Cpf =>
                TryAlternativeFormat(field, profile.Cpf, SemanticFieldType.Cpf),
            1 when inferredType == SemanticFieldType.Salary =>
                TryAlternativeFormat(field, profile.SalaryExpectation, SemanticFieldType.Salary),

            // Tentativa 2: tentar outro campo do perfil relacionado
            2 when inferredType == SemanticFieldType.FirstName && !string.IsNullOrWhiteSpace(profile.FullName) =>
                new FieldResolution(SemanticProfileMapper.ExtractFirstName(profile.FullName), SemanticFieldType.FirstName, 0.5m, "alternative:first-name", false),
            2 when inferredType == SemanticFieldType.LastName && !string.IsNullOrWhiteSpace(profile.FullName) =>
                new FieldResolution(SemanticProfileMapper.ExtractLastName(profile.FullName), SemanticFieldType.LastName, 0.5m, "alternative:last-name", false),

            _ => null
        };
    }

    private static FieldResolution? TryAlternativeFormat(ElementCandidate field, string value, SemanticFieldType type)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var formatted = type switch
        {
            SemanticFieldType.Phone => SemanticProfileMapper.FormatPhone(value, field.Placeholder, field.DataMask),
            SemanticFieldType.Cpf => SemanticProfileMapper.FormatCpf(value),
            SemanticFieldType.Salary => SemanticProfileMapper.NormalizeSalary(value, field),
            _ => value
        };

        if (formatted != value)
            return new FieldResolution(formatted, type, 0.5m, $"alternative-format:{type}", false);

        return null;
    }

    private static string? TryReusableAnswer(ElementCandidate field, List<ReusableAnswer> answers)
    {
        var normalized = (field.Label + " " + field.Name).ToLowerInvariant();
        return answers.FirstOrDefault(a =>
            normalized.Contains(a.QuestionKey.ToLowerInvariant()) ||
            normalized.Contains(a.QuestionText.ToLowerInvariant()))?.AnswerValue;
    }

    private static string? TryPattern(ElementCandidate field, List<QuestionPattern> patterns, CandidateProfile? profile, List<ReusableAnswer> answers)
    {
        var normalized = field.Label.ToLowerInvariant();

        foreach (var pattern in patterns)
        {
            var templates = pattern.QuestionTemplates;
            if (templates.Any(t => AgentText.ContainsAny(normalized, [t.ToLowerInvariant()])) ||
                AgentText.ContainsAny(normalized, pattern.Tags.Select(t => t.ToLowerInvariant()).ToList()))
            {
                if (pattern.ValueSource == "profile_field" && profile is not null && !string.IsNullOrWhiteSpace(pattern.ProfileFieldMapping))
                {
                    var resolved = FormFiller.ResolveValue(pattern.ProfileFieldMapping, profile, answers);
                    if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
                }
                if (pattern.ValueSource == "static_value")
                    return pattern.AnswerTemplate;
            }
        }
        return null;
    }
}

/// <summary>
/// Resultado de uma resolução de campo com metadados de confiança.
/// </summary>
public sealed record FieldResolution(
    string? Value,
    SemanticFieldType InferredType,
    decimal Confidence,
    string Reason,
    bool IsConfident);
