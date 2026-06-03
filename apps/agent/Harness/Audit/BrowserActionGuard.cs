using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Harness.Audit;

public static class BrowserActionGuard
{
    public static ActionVerdict Evaluate(DecisionFrame frame, ActionProposal proposal, ActionRisk risk)
    {
        var action = proposal.ToBrowserAction();
        var guardrails = new List<string>();

        if (!TargetExistsWhenRequired(frame.Observation.Observation, action, out var targetReason))
            return Block("target_not_observed", targetReason, guardrails);
        guardrails.Add("target_observed_or_not_required");

        if (!ActionMatchesTarget(frame.Observation.Observation, action, out var matchReason))
            return Block("target_type_incompatible", matchReason, guardrails);
        guardrails.Add("action_type_matches_target");

        if (BlocksNavigation(frame.Observation.Observation, action, out var navigationReason))
            return Block("navigation_blocked_by_required_fields", navigationReason, guardrails);
        guardrails.Add("navigation_required_fields_clear");

        if (risk.RequiresCheckpoint)
            return Block("risk_requires_checkpoint", string.Join("; ", risk.Reasons), guardrails);

        return new ActionVerdict(
            true,
            "approved",
            "Action approved by harness guard.",
            risk,
            new ApprovedAction(proposal, risk, "BrowserActionGuard", DateTimeOffset.UtcNow),
            guardrails.ToArray(),
            false);

        ActionVerdict Block(string verdict, string reason, List<string> collectedGuardrails) =>
            new(
                false,
                verdict,
                reason,
                risk,
                null,
                collectedGuardrails.ToArray(),
                true);
    }

    public static bool IsSafeAction(PageObservation observation, BrowserAction action, out string reason)
    {
        if (!TargetExistsWhenRequired(observation, action, out reason))
            return false;

        if (!ActionMatchesTarget(observation, action, out reason))
            return false;

        if (BlocksNavigation(observation, action, out reason))
            return false;

        reason = "";
        return true;
    }

    public static bool TargetExistsWhenRequired(PageObservation observation, BrowserAction action, out string reason)
    {
        if (action.Type is "scroll" or "wait" or "done" or "checkpoint")
        {
            reason = "";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(action.ElementId) &&
            observation.Elements.Any(e => e.ElementId == action.ElementId))
        {
            reason = "";
            return true;
        }

        reason = $"Action target was not observed: type={action.Type}; element={action.ElementId}";
        return false;
    }

    public static bool ActionMatchesTarget(PageObservation observation, BrowserAction action, out string reason)
    {
        if (action.Type is "scroll" or "wait" or "done" or "checkpoint")
        {
            reason = "";
            return true;
        }

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (element is null)
        {
            reason = $"Action target was not observed: {action.ElementId}";
            return false;
        }

        var valid = action.Type switch
        {
            "fill" => element.IsTextEntry,
            "select" => element.IsSelect,
            "check" => element.IsChoice,
            "upload_file" => element.IsFile,
            "click" or "submit" => element.IsButtonLike,
            _ => false
        };

        if (valid)
        {
            reason = "";
            return true;
        }

        reason = $"Action '{action.Type}' is incompatible with observed element '{element.Label}' ({element.Tag}/{element.Type}/{element.Role}).";
        return false;
    }

    public static bool BlocksNavigation(PageObservation observation, BrowserAction action, out string reason)
    {
        if (!IsNavigationAction(observation, action))
        {
            reason = "";
            return false;
        }

        return HasBlockingRequiredIssue(observation, out reason);
    }

    public static bool HasBlockingRequiredIssue(PageObservation observation, out string reason)
    {
        foreach (var field in observation.Elements.Where(e => e.IsField && e.Required && !e.IsDisabledState))
        {
            if (field.IsChoice && !field.Checked)
            {
                reason = $"Required choice is unanswered: {field.Label}";
                return true;
            }

            if (field.IsSelect && FieldValuePolicy.IsEmptySelectValue(field))
            {
                reason = $"Required select/combobox is still empty: {field.Label}";
                return true;
            }

            if (field.IsTextEntry && !field.IsFile && string.IsNullOrWhiteSpace(field.Value))
            {
                reason = $"Required text field is empty: {field.Label}";
                return true;
            }

            if (field.IsFile && !(LooksLikeResumeField(field) && ResumeAlreadySelected(observation)))
            {
                reason = $"Required file input is pending: {field.Label}";
                return true;
            }
        }

        var visibleError = VisibleValidationError(observation.VisibleText);
        if (!string.IsNullOrWhiteSpace(visibleError))
        {
            reason = $"Visible validation error is present: {visibleError}";
            return true;
        }

        reason = "";
        return false;
    }

    private static bool IsNavigationAction(PageObservation observation, BrowserAction action)
    {
        if (action.Type == "submit") return true;
        if (action.Type != "click") return false;

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (element is null) return false;

        if (AgentText.ContainsAny(element.Text, ["voltar", "back", "previous", "etapa anterior"]))
            return false;

        return PageStateClassifier.LooksLikeNextElement(element) ||
               PageStateClassifier.LooksLikeSubmitElement(element);
    }

    private static string VisibleValidationError(string visibleText)
    {
        var lines = visibleText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.FirstOrDefault(line => AgentText.ContainsAny(line, [
            "insira uma resposta valida", "insira uma resposta válida", "enter a valid answer",
            "campo obrigatorio", "campo obrigatório", "required", "please enter",
            "please select", "selecione uma opcao", "selecione uma opção"
        ])) ?? "";
    }

    private static bool ResumeAlreadySelected(PageObservation observation) =>
        observation.Elements.Any(e => e.IsFile && !string.IsNullOrWhiteSpace(e.Value)) ||
        AgentText.ContainsAny(observation.VisibleText, [
            ".pdf", ".doc", ".docx", "curriculo nicolas", "curriculo", "resume",
            "arquivo foi carregado", "carregado com sucesso", "upload concluido", "upload concluído",
            "file uploaded", "uploaded successfully",
            "ultima utilizacao", "ultima utilização", "desmarcar selecao", "desmarcar seleção"
        ]);

    private static bool LooksLikeResumeField(ElementCandidate field) =>
        AgentText.ContainsAny(field.SearchTextWithHints, ["curriculo", "currículo", "resume", "cv"]);
}
