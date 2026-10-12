using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Audit;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Application;

/// <summary>
/// Decision planner with deterministic rules around the LLM. The LLM proposes
/// one atomic action; the runtime guard is the only component that approves it.
/// </summary>
public sealed class LlmDecisionEngine
{
    private readonly LlmClient _llm;
    private readonly DecisionEngine _local;
    private readonly LocalAnswerResolver _resolver;
    private readonly AgentPolicy _policy;

    public LlmDecisionEngine(LlmClient llm, LocalAnswerResolver resolver, AgentPolicy policy)
    {
        _llm = llm;
        _resolver = resolver;
        _policy = policy;
        _local = new DecisionEngine(resolver, policy);
    }

    public async Task<BrowserAction?> DecideAsync(
        PageObservation observation,
        AgentTaskResponse task,
        string jobContext,
        SessionContext? ctx = null)
    {
        ctx ??= new SessionContext();
        var pageStateClassifier = new PageStateClassifier();
        var valuePolicy = new FieldValuePolicy(_resolver);
        var audit = new FormAuditor(pageStateClassifier, valuePolicy).Audit(observation, task);
        var platform = PlatformFromUrl(observation.Url);
        var frame = new DecisionFrame(
            new AgentObjective(
                "apply",
                "Fill the job application form safely and submit only when policy allows it.",
                _policy.AutoSubmitMode.Equals("aggressive", StringComparison.OrdinalIgnoreCase),
                task.Lead.Url,
                _policy.AutoSubmitMode),
            new ObservationFrame(observation, platform, platform, pageStateClassifier.Classify(observation), observation.Fingerprint(), DateTimeOffset.UtcNow),
            audit,
            jobContext,
            _policy.AutoSubmitMode);

        var proposal = await ProposeAsync(frame, task, ctx);
        if (proposal is null) return null;

        var action = proposal.ToBrowserAction();
        if (!BrowserActionGuard.IsSafeAction(observation, action, out var unsafeReason))
            return BrowserAction.Checkpoint(action.ElementId, action.Label, $"Decision blocked by harness guard: {unsafeReason}");

        return action;
    }

    public async Task<ActionProposal?> ProposeAsync(
        DecisionFrame frame,
        AgentTaskResponse task,
        SessionContext? ctx = null)
    {
        ctx ??= new SessionContext();

        var requiredQuestionGuard = await _local.ProposeAsync(frame, task, ctx);
        if (IsRequiredQuestionGuardProposal(requiredQuestionGuard))
        {
            return requiredQuestionGuard! with
            {
                Planner = "local_required_guard",
                Reason = $"Required-question guard: {requiredQuestionGuard.Reason}"
            };
        }

        if (IsSubmitBoundaryProposal(requiredQuestionGuard))
        {
            return requiredQuestionGuard! with
            {
                Planner = "local_submit_guard",
                Reason = $"Submit-boundary guard: {requiredQuestionGuard.Reason}"
            };
        }

        if (_llm.Enabled)
        {
            var observation = frame.Observation.Observation;
            string? previousFailure = null;

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var objective = previousFailure is null
                    ? frame.Objective.Description
                    : $"Repair the previous invalid decision and choose a safe observed element. Previous failure: {previousFailure}";
                var llmPlan = await _llm.PlanBrowserActionAsync(
                    observation,
                    frame.JobContext,
                    frame.Observation.Phase,
                    objective,
                    task.Task.Id,
                    attempt,
                    previousFailure,
                    ctx.DecisionMemorySummary());
                if (llmPlan is null)
                {
                    previousFailure = "LLM returned no plan.";
                    continue;
                }

                if (llmPlan.RequiresCheckpoint)
                {
                    if (IsRepairablePlannerCheckpoint(llmPlan.Reason))
                    {
                        previousFailure = llmPlan.Reason;
                        continue;
                    }

                    return ActionProposal.FromAction(
                        BrowserAction.Checkpoint(llmPlan.ElementId, llmPlan.ElementId, $"LLM checkpoint: {llmPlan.Reason}"),
                        "llm",
                        requiresCheckpoint: true);
                }

                if (llmPlan.Confidence >= 0.55m && IsValidActionType(llmPlan.ActionType))
                {
                    return new ActionProposal(
                        llmPlan.ActionType,
                        llmPlan.ElementId,
                        llmPlan.Value,
                        FindElementLabel(observation, llmPlan.ElementId),
                        $"LLM: {llmPlan.Reason}",
                        llmPlan.Confidence,
                        "llm",
                        InferExpectedEffect(llmPlan.ActionType));
                }

                previousFailure = $"Invalid or low-confidence plan. action={llmPlan.ActionType}; confidence={llmPlan.Confidence:0.00}; reason={llmPlan.Reason}";
            }
        }

        var localAction = await _local.ProposeAsync(frame, task, ctx);
        return localAction is null
            ? null
            : localAction with { Planner = "local_fallback", Reason = $"Local fallback after LLM decision loop failed. {localAction.Reason}" };
    }

    public bool CanSubmitAutomatically(PageObservation observation) =>
        _local.CanSubmitAutomatically(observation);

    public static string ClassifyPageState(PageObservation observation) =>
        DecisionEngine.ClassifyPageState(observation);

    public static bool IsConfirmation(PageObservation observation) =>
        DecisionEngine.IsConfirmation(observation);

    public static bool IsBlockedState(PageObservation observation) =>
        DecisionEngine.IsBlockedState(observation);

    public static bool IsSensitiveQuestion(string text) =>
        DecisionEngine.IsSensitiveQuestion(text);

    private static bool IsValidActionType(string actionType) =>
        actionType is "click" or "fill" or "select" or "check" or "upload_file"
            or "scroll" or "wait" or "submit"
            or "checkpoint" or "done";

    private static bool IsRepairablePlannerCheckpoint(string reason) =>
        AgentText.ContainsAny(reason, [
            "not present in the current observation",
            "incompatible with observed element",
            "unsupported action type",
            "tried to advance while required fields are still invalid"
        ]);

    private static bool IsRequiredQuestionGuardProposal(ActionProposal? proposal)
    {
        if (proposal is null) return false;
        if (proposal.Type is "fill" or "select" or "check" or "upload_file") return true;
        if (proposal.Type == "scroll" && proposal.Reason.Contains("audit lower questions", StringComparison.OrdinalIgnoreCase)) return true;
        if (proposal.Type == "checkpoint" &&
            AgentText.ContainsAny(proposal.Reason, [
                "required", "obrigatorio", "obrigatoria", "sensitive", "eligibility",
                "select question", "radio", "checkbox", "answer source", "resume"
            ]))
            return true;

        return false;
    }

    private static bool IsSubmitBoundaryProposal(ActionProposal? proposal)
    {
        if (proposal is null) return false;
        if (proposal.Type == "submit") return true;
        return proposal.Type == "checkpoint" &&
               AgentText.ContainsAny(proposal.Reason, ["submit detected", "policy requires review", "final submit"]);
    }

    private static string FindElementLabel(PageObservation observation, string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId)) return "";
        var element = observation.Elements.FirstOrDefault(e => e.ElementId == elementId);
        return element?.Label ?? element?.Text ?? elementId;
    }

    private static string InferExpectedEffect(string actionType) => actionType switch
    {
        "fill" => "field_value_changes",
        "select" => "selection_changes",
        "check" => "choice_checked",
        "upload_file" => "file_attached",
        "click" => "page_or_surface_changes",
        "submit" => "application_submitted_or_confirmation_visible",
        "scroll" => "new_content_visible",
        "wait" => "page_stabilizes",
        "checkpoint" => "human_review_requested",
        _ => "unknown"
    };

    private static string PlatformFromUrl(string url) =>
        url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) ? "LinkedIn" : "Generic";
}
