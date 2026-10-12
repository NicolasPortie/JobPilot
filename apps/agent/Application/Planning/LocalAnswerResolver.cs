using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Application;

public sealed class LocalAnswerResolver
{
    private readonly JobPilotApiClient _client;
    private readonly SemanticFieldResolver _semantic = new();
    private CandidateProfile? _profile;
    private List<ReusableAnswer> _answers = [];
    private List<QuestionPattern> _patterns = [];
    private bool _loaded;

    public LocalAnswerResolver(JobPilotApiClient client) => _client = client;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _answers = await _client.GetReusableAnswersAsync();
        _patterns = await _client.GetQuestionPatternsAsync();
        _loaded = true;
    }

    public void SetProfile(CandidateProfile? profile, string? resumePathOverride = null)
    {
        _profile = profile;
        if (_profile is not null && !string.IsNullOrWhiteSpace(resumePathOverride))
            _profile.ResumePath = resumePathOverride;
    }

    /// <summary>
    /// Resolve usando o novo motor semântico (inferência de tipo + profile mapping + session context).
    /// </summary>
    public FieldResolution? ResolveSemantic(ElementCandidate field, SessionContext ctx)
    {
        if (_profile is null) return null;
        return _semantic.Resolve(field, _profile, ctx, _answers, _patterns, "");
    }

    /// <summary>
    /// Tenta uma resolução alternativa quando a principal falhou validação.
    /// </summary>
    public FieldResolution? ResolveAlternative(ElementCandidate field, SessionContext ctx, int attempt)
    {
        if (_profile is null) return null;
        return _semantic.ResolveAlternative(field, _profile, ctx, _answers, _patterns, attempt);
    }

    /// <summary>
    /// Método legado: resolve por texto (usado para choices e fallback).
    /// </summary>
    public string? Resolve(string questionText, string fieldContext, string jobContext)
    {
        var normalized = questionText.ToLowerInvariant();

        // 1. Reusable answers
        var answer = _answers.FirstOrDefault(x =>
            normalized.Contains(x.QuestionKey.ToLowerInvariant()) ||
            normalized.Contains(x.QuestionText.ToLowerInvariant()));
        if (answer is not null) return answer.AnswerValue;

        // 2. Profile fields
        if (_profile is not null)
        {
            var profileValue = FormFiller.ResolveValue(questionText, _profile, _answers);
            if (!string.IsNullOrWhiteSpace(profileValue)) return profileValue;
        }

        // 3. Question patterns
        foreach (var pattern in _patterns)
        {
            var templates = pattern.QuestionTemplates;
            if (templates.Any(t => AgentText.ContainsAny(normalized, [t.ToLowerInvariant()])) ||
                AgentText.ContainsAny(normalized, pattern.Tags.Select(t => t.ToLowerInvariant()).ToList()))
            {
                var resolved = ResolvePatternValue(pattern, jobContext);
                if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
            }
        }

        return null;
    }

    public async Task<string?> DraftAsync(string questionText, string jobContext)
    {
        // Fase 4: aqui será chamado o LLM local
        // Por enquanto, usa fallback da API
        return await _client.DraftAnswerAsync(questionText, jobContext);
    }

    private string? ResolvePatternValue(QuestionPattern pattern, string jobContext)
    {
        if (pattern.ValueSource == "profile_field" && _profile is not null && !string.IsNullOrWhiteSpace(pattern.ProfileFieldMapping))
        {
            return FormFiller.ResolveValue(pattern.ProfileFieldMapping, _profile, _answers);
        }
        if (pattern.ValueSource == "static_value")
        {
            return pattern.AnswerTemplate;
        }
        if (pattern.ValueSource == "ai_draft")
        {
            // Fase 4: chamar LLM
            return null;
        }
        return null;
    }
}
