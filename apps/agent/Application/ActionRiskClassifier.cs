using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public sealed class ActionRiskClassifier
{
    public ActionRisk Classify(DecisionFrame frame, ActionProposal proposal, ObservedContentSafetyReport contentSafety)
    {
        var budget = frame.Objective.AllowSubmit
            ? RiskBudget.FromPolicy(new AgentPolicy("aggressive", false, 10, 50, [], []), frame.Observation.Platform)
            : RiskBudget.FromPolicy(new AgentPolicy(frame.Objective.PolicyMode, false, 10, 50, [], []), frame.Observation.Platform);

        var reasons = new List<string>();
        var level = RiskLevel.Low;
        var submitBoundary = IsSubmitBoundary(frame.Observation.Observation, proposal);
        var requiresCheckpoint = proposal.RequiresCheckpoint;

        if (proposal.Type == "checkpoint")
        {
            return new ActionRisk(RiskLevel.High, ["proposal_requires_checkpoint"], submitBoundary, true, budget);
        }

        if (submitBoundary)
        {
            level = RiskLevel.High;
            reasons.Add("submit_boundary");
            if (!budget.AllowSubmit)
                requiresCheckpoint = true;
        }

        if (proposal.Type == "upload_file")
        {
            level = Max(level, RiskLevel.Medium);
            reasons.Add("file_upload");
        }

        if (proposal.Type == "fill")
        {
            var element = frame.Observation.Observation.Elements.FirstOrDefault(e => e.ElementId == proposal.ElementId);
            if (element is not null && element.IsSensitive)
            {
                level = Max(level, RiskLevel.High);
                reasons.Add("sensitive_field");
                if (!HasExplicitAnswerSource(proposal) && !budget.AllowSensitiveWithoutSource)
                    requiresCheckpoint = true;
            }
        }

        if (frame.Observation.Platform.Equals("Generic", StringComparison.OrdinalIgnoreCase) && submitBoundary && !budget.AllowGenericSubmit)
        {
            reasons.Add("generic_platform_submit_requires_review");
            requiresCheckpoint = true;
        }

        if (contentSafety.HasSignals)
        {
            level = Max(level, contentSafety.RiskLevel);
            reasons.Add("observed_content_safety_signal");
            if (contentSafety.HasPromptInjectionSignals && proposal.Type is not ("scroll" or "wait"))
                requiresCheckpoint = true;
        }

        if (level > budget.MaxAutoRisk)
        {
            reasons.Add($"risk_budget_exceeded:{budget.MaxAutoRisk}");
            requiresCheckpoint = true;
        }

        if (reasons.Count == 0)
            reasons.Add("low_risk_observed_action");

        return new ActionRisk(level, reasons.Distinct().ToArray(), submitBoundary, requiresCheckpoint, budget);
    }

    private static bool IsSubmitBoundary(PageObservation observation, ActionProposal proposal)
    {
        if (proposal.Type == "submit") return true;
        if (proposal.Type != "click") return false;
        var target = observation.Elements.FirstOrDefault(e => e.ElementId == proposal.ElementId);
        return target is not null && PageStateClassifier.LooksLikeSubmitElement(target);
    }

    private static bool HasExplicitAnswerSource(ActionProposal proposal) =>
        AgentText.ContainsAny($"{proposal.Reason} {proposal.ExpectedEffect}", [
            "candidate-profile", "profile", "reusable-answer", "question-pattern", "explicit", "saved answer"
        ]);

    private static RiskLevel Max(RiskLevel left, RiskLevel right) =>
        (RiskLevel)Math.Max((int)left, (int)right);
}

public sealed class RecoveryEngine
{
    public RecoveryDecision Decide(ActionOutcome outcome, ActionVerdict verdict)
    {
        if (verdict.RequiresCheckpoint)
        {
            return new RecoveryDecision(
                RecoveryMode.Checkpoint,
                verdict.Reason,
                false,
                "Review the blocked action and resolve the checkpoint before continuing.",
                "reobserve");
        }

        if (outcome.Success)
            return new RecoveryDecision(RecoveryMode.None, outcome.Message, true);

        var failure = outcome.Failure;
        if (failure?.Retriable == true)
            return new RecoveryDecision(RecoveryMode.Retry, failure.Detail, true, "", "retry_action");

        var code = outcome.FailureCode ?? failure?.Code ?? "";
        if (code.Contains("resume_upload_requires_manual_selection", StringComparison.OrdinalIgnoreCase))
        {
            return new RecoveryDecision(
                RecoveryMode.Checkpoint,
                outcome.Message,
                false,
                "Anexe o curriculo manualmente no navegador e retome a execucao.",
                "reobserve");
        }

        if (code.Contains("validation", StringComparison.OrdinalIgnoreCase))
        {
            return new RecoveryDecision(
                RecoveryMode.Checkpoint,
                outcome.Message,
                false,
                "Review visible validation messages and provide missing or corrected data.",
                "reobserve");
        }

        if (code.Contains("not_observed", StringComparison.OrdinalIgnoreCase) || code.Contains("target", StringComparison.OrdinalIgnoreCase))
            return new RecoveryDecision(RecoveryMode.Reobserve, outcome.Message, true, "", "reobserve");

        return new RecoveryDecision(
            RecoveryMode.Checkpoint,
            outcome.Message,
            false,
            "Review the browser state before the agent continues.",
            "reobserve");
    }
}
