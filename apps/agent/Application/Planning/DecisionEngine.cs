using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public sealed class DecisionEngine
{
    private readonly AgentPolicy _policy;
    private readonly PageStateClassifier _pageStateClassifier;
    private readonly FormAuditor _formAuditor;
    private readonly ActionPlanner _actionPlanner;

    public DecisionEngine(LocalAnswerResolver resolver, AgentPolicy policy)
    {
        _policy = policy;
        _pageStateClassifier = new PageStateClassifier();
        var valuePolicy = new FieldValuePolicy(resolver);
        _formAuditor = new FormAuditor(_pageStateClassifier, valuePolicy);
        _actionPlanner = new ActionPlanner(valuePolicy, _pageStateClassifier);
    }

    public async Task<BrowserAction?> DecideAsync(PageObservation observation, AgentTaskResponse task, string jobContext, SessionContext? ctx = null)
    {
        var proposal = await ProposeAsync(observation, task, jobContext, ctx);
        return proposal?.ToBrowserAction();
    }

    public async Task<ActionProposal?> ProposeAsync(PageObservation observation, AgentTaskResponse task, string jobContext, SessionContext? ctx = null)
    {
        ctx ??= new SessionContext();
        var audit = _formAuditor.Audit(observation, task);
        var plan = await _actionPlanner.PlanAsync(observation, audit, task, jobContext, ctx, _policy);
        return plan is null ? null : plan.Proposal with { Planner = "local" };
    }

    public async Task<ActionProposal?> ProposeAsync(DecisionFrame frame, AgentTaskResponse task, SessionContext? ctx = null)
    {
        ctx ??= new SessionContext();
        var plan = await _actionPlanner.PlanAsync(
            frame.Observation.Observation,
            frame.Audit,
            task,
            frame.JobContext,
            ctx,
            _policy);
        return plan is null ? null : plan.Proposal with { Planner = "local" };
    }

    public bool CanSubmitAutomatically(PageObservation observation) =>
        _pageStateClassifier.CanSubmitAutomatically(observation, _policy);

    public static string ClassifyPageState(PageObservation observation) =>
        new PageStateClassifier().Classify(observation);

    public static bool IsConfirmation(PageObservation observation) =>
        PageStateClassifier.IsConfirmation(observation);

    public static bool IsBlockedState(PageObservation observation) =>
        PageStateClassifier.IsBlockedState(observation);

    public static bool IsSensitiveQuestion(string text)
    {
        var normalized = AgentText.RemoveDiacritics(text).ToLowerInvariant();
        var words = normalized.Split([' ', '-', '_', '?', '.', ',', '(', ')', '[', ']', '/', '\'', '"'], StringSplitOptions.RemoveEmptyEntries);
        var sensitiveWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "visa", "visto", "authorization", "autorizacao",
            "disability", "deficiencia", "race", "raca",
            "gender", "genero", "criminal", "antecedentes",
            "salary", "history", "pagamento", "rg", "cpf", "passport", "passaporte",
            "nationality", "nacionalidade", "citizenship", "cidadania", "religion", "religiao"
        };
        return words.Any(w => sensitiveWords.Contains(w));
    }
}
