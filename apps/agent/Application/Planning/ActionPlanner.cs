using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public sealed class ActionPlanner
{
    private readonly FieldValuePolicy _valuePolicy;
    private readonly PageStateClassifier _pageStateClassifier;

    public ActionPlanner(FieldValuePolicy valuePolicy, PageStateClassifier pageStateClassifier)
    {
        _valuePolicy = valuePolicy;
        _pageStateClassifier = pageStateClassifier;
    }

    public async Task<ActionPlan?> PlanAsync(PageObservation observation, FormAuditResult audit, AgentTaskResponse task, string jobContext, SessionContext ctx, AgentPolicy policy)
    {
        if (PageStateClassifier.IsConfirmation(observation))
            return Plan(new BrowserAction("done", "", "", "", "Confirmation page detected.", 0.95m), "none");

        if (PageStateClassifier.IsBlockedState(observation))
            return Plan(BrowserAction.Checkpoint("", "", "Security, captcha, or login challenge detected."), "human-required");

        var unansweredChoice = audit.Issues.FirstOrDefault(i => i.Field.IsChoice && i.IssueType == ReasonCodes.MissingRequiredField)?.Field;
        if (unansweredChoice is not null)
        {
            var sourceValue = _valuePolicy.ResolveChoiceValue(unansweredChoice, jobContext);

            if (!string.IsNullOrWhiteSpace(sourceValue))
            {
                var matchingChoice = observation.Elements
                    .Where(e => e.IsChoice && e.Required && !e.Checked && !e.IsDisabledState)
                    .FirstOrDefault(e => ChoiceMatches(e, sourceValue));
                if (matchingChoice is not null)
                    return Plan(new BrowserAction("check", matchingChoice.ElementId, "", matchingChoice.Label, "Mapped required choice from explicit reusable/profile answer.", 0.82m), "low");
            }

            return Plan(BrowserAction.Checkpoint(unansweredChoice.ElementId, unansweredChoice.Label, "Required radio/checkbox question has no answer source."), "human-required");
        }

        var emptySelect = audit.Issues.FirstOrDefault(i => i.Field.IsSelect && i.IssueType == ReasonCodes.MissingRequiredField)?.Field;
        if (emptySelect is not null)
        {
            var value = _valuePolicy.ResolveSelectValue(emptySelect, task, jobContext, ctx);
            if (!string.IsNullOrWhiteSpace(value))
                return Plan(new BrowserAction("select", emptySelect.ElementId, value, emptySelect.Label,
                    "Mapped select option from candidate profile or reusable answer.", 0.78m), "low");

            if (emptySelect.Required)
                return Plan(BrowserAction.Checkpoint(emptySelect.ElementId, emptySelect.Label, "Required select question has no matching option."), "human-required");
        }

        var invalidProfileField = audit.Issues.FirstOrDefault(i => i.IssueType == ReasonCodes.InvalidExistingValue)?.Field;
        if (invalidProfileField is not null)
        {
            var correction = _valuePolicy.ResolveExistingCorrection(invalidProfileField, task.Profile);
            if (correction is not null)
                return Plan(new BrowserAction("fill", invalidProfileField.ElementId, correction.Value, invalidProfileField.Label, correction.Reason, correction.Confidence), "low");
        }

        var emptyField = audit.Issues.FirstOrDefault(i => i.Field.IsTextEntry && !i.Field.IsFile && i.IssueType == ReasonCodes.MissingRequiredField)?.Field;
        if (emptyField is not null)
        {
            var resolution = await _valuePolicy.ResolveTextAsync(emptyField, task, jobContext, ctx, policy);
            if (resolution is not null)
            {
                if (resolution.InferredType != SemanticFieldType.Unknown)
                    ctx.Record(emptyField.ElementId, resolution.Value, resolution.InferredType);

                return Plan(new BrowserAction("fill", emptyField.ElementId, resolution.Value, emptyField.Label, resolution.Reason, resolution.Confidence),
                    resolution.RequiresReview ? "needs-review" : "low");
            }

            if (DecisionEngine.IsSensitiveQuestion(emptyField.SearchText))
                return Plan(BrowserAction.Checkpoint(emptyField.ElementId, emptyField.Label, "Sensitive or eligibility field has no explicit reusable/profile source."), "human-required");

            if (IsRequiredProfileUrlField(emptyField))
                return Plan(BrowserAction.Checkpoint(emptyField.ElementId, emptyField.Label, "Required profile URL field has no configured profile source."), "human-required");
        }

        var upload = audit.Issues.FirstOrDefault(i => i.Field.IsFile)?.Field;
        if (upload is not null)
        {
            if (!LooksLikeResumeUpload(upload))
                return Plan(BrowserAction.Checkpoint(upload.ElementId, upload.Label, "Required file upload is not a resume field and needs human review."), "human-required");

            var resumePath = task.ResolveResumePath();
            if (string.IsNullOrWhiteSpace(resumePath))
                return Plan(BrowserAction.Checkpoint(upload.ElementId, upload.Label, "Resume path is not configured for this application or candidate profile."), "human-required");

            if (!File.Exists(resumePath))
                return Plan(BrowserAction.Checkpoint(upload.ElementId, upload.Label, $"Resume file was not found at {resumePath}."), "human-required");
            return Plan(new BrowserAction("upload_file", upload.ElementId, resumePath, upload.Label, "Upload resume into observed file field.", 0.95m), "low");
        }

        var easyApply = audit.PageState is not ("easy_apply_modal" or "form" or "external_application")
            ? observation.Elements.FirstOrDefault(e => e.IsButtonLike && PageStateClassifier.LooksLikeApply(e.SearchText) && !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText))
            : null;
        if (easyApply is not null)
            return Plan(new BrowserAction("click", easyApply.ElementId, "", easyApply.Label, "Open application flow from observed apply control.", 0.86m), "low");

        if (ShouldAuditBelowBeforeAdvancing(observation, audit))
            return Plan(new BrowserAction("scroll", "", "", "", "Visible fields are handled; scroll to audit lower questions before advancing or submitting.", 0.82m), "low");

        var submit = observation.Elements.FirstOrDefault(e => e.IsButtonLike && PageStateClassifier.LooksLikeSubmitElement(e) && !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText));
        if (submit is not null)
        {
            if (!_pageStateClassifier.CanSubmitAutomatically(observation, policy))
                return Plan(BrowserAction.Checkpoint(submit.ElementId, submit.Label, "Submit detected but policy requires review."), "needs-review");
            return Plan(new BrowserAction("submit", submit.ElementId, "", submit.Label, "Observed final submit/review action.", 0.82m), "medium");
        }

        var next = observation.Elements.FirstOrDefault(e => e.IsButtonLike && PageStateClassifier.LooksLikeNextElement(e) && !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText));
        if (next is not null)
            return Plan(new BrowserAction("click", next.ElementId, "", next.Label, "Advance to the next observed application step.", 0.8m), "low");

        if (observation.Elements.Count(e => e.IsButtonLike) == 0)
            return Plan(new BrowserAction("scroll", "", "", "", "No button-like controls visible; scroll for more fields/actions.", 0.7m), "low");

        return null;
    }

    private static ActionPlan Plan(BrowserAction action, string risk) => new(action, risk);

    private static bool ShouldAuditBelowBeforeAdvancing(PageObservation observation, FormAuditResult audit)
    {
        if (!observation.CanScrollDown)
            return false;

        if (audit.PageState is not ("easy_apply_modal" or "form" or "external_application"))
            return false;

        if (audit.Issues.Any(i => i.IssueType is ReasonCodes.MissingRequiredField or ReasonCodes.InvalidExistingValue))
            return false;

        return observation.Elements.Any(e =>
            e.IsButtonLike &&
            (PageStateClassifier.LooksLikeNextElement(e) || PageStateClassifier.LooksLikeSubmitElement(e)) &&
            !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText));
    }

    private static bool ChoiceMatches(ElementCandidate element, string answer)
    {
        var target = AgentText.RemoveDiacritics(answer).ToLowerInvariant();
        var text = AgentText.RemoveDiacritics(element.SearchText).ToLowerInvariant();
        if (target is "sim" or "yes" or "true") return AgentText.ContainsAny(text, ["sim", "yes"]);
        if (target is "nao" or "não" or "no" or "false") return AgentText.ContainsAny(text, ["nao", "não", "no"]);
        return text.Contains(target);
    }

    private static bool LooksLikeResumeUpload(ElementCandidate element) =>
        AgentText.ContainsAny(element.SearchTextWithHints, ["curriculo", "currículo", "resume", "cv"]);

    private static bool IsRequiredProfileUrlField(ElementCandidate element) =>
        element.Required && AgentText.ContainsAny(element.SearchTextWithHints, ["linkedin", "github", "portfolio", "portifolio", "website", "site"]);
}
