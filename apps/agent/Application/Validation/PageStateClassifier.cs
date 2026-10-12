using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Audit;

namespace JobPilot.Agent.Application;

public sealed class PageStateClassifier
{
    public string Classify(PageObservation observation)
    {
        if (IsBlockedState(observation)) return "security";
        if (IsConfirmation(observation)) return "confirmation";
        if (observation.HasModal && observation.Url.Contains("linkedin.com/jobs", StringComparison.OrdinalIgnoreCase))
            return "easy_apply_modal";
        if (observation.Url.Contains("/jobs/search", StringComparison.OrdinalIgnoreCase)) return "search_results";
        if (observation.Url.Contains("/jobs/view/", StringComparison.OrdinalIgnoreCase))
        {
            if (observation.Elements.Any(e => e.IsButtonLike && LooksLikeApply(e.SearchText)))
                return "job_detail";
        }
        if (!observation.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) && observation.Elements.Any(e => e.IsField))
            return "external_application";
        if (observation.Elements.Any(e => e.IsField)) return "form";
        if (observation.Elements.Any(e => e.IsButtonLike && LooksLikeSubmitElement(e))) return "form";
        return "unknown";
    }

    public bool CanSubmitAutomatically(PageObservation observation, AgentPolicy policy)
    {
        if (policy.AutoSubmitMode.Equals("review_only", StringComparison.OrdinalIgnoreCase)) return false;
        if (AgentText.ContainsAny(observation.VisibleText, ["payment", "pagamento", "captcha", "2fa", "verification code"])) return false;
        if (policy.AutoSubmitMode.Equals("aggressive", StringComparison.OrdinalIgnoreCase)) return true;
        return !BrowserActionGuard.HasBlockingRequiredIssue(observation, out _);
    }

    public static bool IsConfirmation(PageObservation observation)
    {
        if (observation.Url.Contains("jobs-tracker", StringComparison.OrdinalIgnoreCase) ||
            observation.Url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase))
            return false;
        if (observation.Url.Contains("post-apply", StringComparison.OrdinalIgnoreCase) ||
            observation.Url.Contains("next-best-action", StringComparison.OrdinalIgnoreCase))
            return true;

        return AgentText.ContainsAny(observation.VisibleText, [
            "application submitted", "candidatura enviada", "aplicacao enviada",
            "your application was sent", "recebemos sua candidatura",
            "foi enviada", "application was sent"]);
    }

    public static bool IsBlockedState(PageObservation observation) =>
        AgentText.ContainsAny(observation.VisibleText, [
            "captcha", "verification code", "two-step", "2-step",
            "security check", "verify your identity", "verifique sua identidade"]);

    public static bool LooksLikeApply(string text) =>
        !AgentText.ContainsAny(text, ["clicou em candidatar-se", "clicked apply", "vaga foi movida"]) &&
        AgentText.ContainsAny(text, ["easy apply", "candidatura simplificada", "aplicar agora", "candidatar-se", "apply now", "apply", "enviar candidatura"]);

    public static bool LooksLikeUnsafeJobPageAction(string text) =>
        AgentText.ContainsAny(text, [
            "clicou em candidatar-se", "clicked apply", "vaga foi movida",
            "enviar mensagem", "send message", "seguir", "follow",
            "salvar", "save", "compartilhar", "share", "denunciar", "report",
            "conheca meus servicos", "conheça meus serviços"]);

    public static bool LooksLikeNextElement(ElementCandidate element)
    {
        if (AgentText.ContainsAny(element.Text, ["voltar", "back", "previous", "etapa anterior"])) return false;
        if (LooksLikeNextSafe(element.Text)) return true;
        if (AgentText.ContainsAny(element.SearchText, ["voltar", "back", "previous", "etapa anterior"])) return false;
        return LooksLikeNextSafe(element.SearchText);
    }

    public static bool LooksLikeSubmitElement(ElementCandidate element)
    {
        if (AgentText.ContainsAny(element.Text, ["voltar", "back", "previous", "etapa anterior"])) return false;
        if (LooksLikeSubmitSafe(element.Text)) return true;
        return LooksLikeSubmitSafe(element.SearchText);
    }

    public static bool LooksLikeReviewEditElement(ElementCandidate element) =>
        element.IsButtonLike &&
        AgentText.ContainsAny(element.SearchText, ["editar", "edit"]) &&
        AgentText.ContainsAny(element.SearchText, ["revise sua candidatura", "review your application", "application review"]);

    public static bool LooksLikeSubmitSafe(string text) =>
        !AgentText.ContainsAny(text, ["voltar", "back", "previous", "etapa anterior"]) &&
        AgentText.ContainsAny(text, ["submit application", "enviar candidatura", "send application", "finalizar candidatura", "submit", "enviar"]);

    private static bool LooksLikeNextSafe(string text) =>
        AgentText.ContainsAny(text, ["next", "continue", "continuar", "avancar", "avançar", "proximo", "próximo", "review", "revisar"]);
}
