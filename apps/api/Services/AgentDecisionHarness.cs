using System.Text.Json;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed record BrowserDecisionPrompt(string System, string User, object Schema);

public sealed record BrowserDecisionValidation(BrowserActionPlanResponse Plan, string? EventType, string? EventDetail);

public static class AgentDecisionHarness
{
    public static readonly object BrowserActionJsonSchema = new
    {
        type = "object",
        properties = new
        {
            actionType = new { type = "string", @enum = new[] { "click", "fill", "select", "check", "upload_file", "scroll", "wait", "submit", "checkpoint", "done" } },
            elementId = new { type = "string" },
            value = new { type = "string" },
            reason = new { type = "string" },
            confidence = new { type = "number", minimum = 0, maximum = 1 },
            requiresCheckpoint = new { type = "boolean" }
        },
        required = new[] { "actionType", "elementId", "value", "reason", "confidence", "requiresCheckpoint" }
        ,
        additionalProperties = false
    };

    public static BrowserDecisionPrompt BuildBrowserActionPrompt(
        BrowserActionPlanRequest request,
        CandidateProfile? profile,
        IReadOnlyList<ReusableAnswer> answers,
        JsonSerializerOptions jsonOptions)
    {
        var system = """
            You are JobPilot's browser decision harness.
            Return only compact JSON matching the provided schema.
            You can only act on elementId values present in the current observation.
            Never invent selectors, coordinates, candidate facts, credentials, degrees, years of experience, visa status, disability, race, gender, or work authorization.
            Never invent resume/CV file paths. For upload_file, choose the observed file input and leave value empty; the local agent owns the configured resume path.
            Action type must match the observed element: fill only text inputs/textareas, select only selects/comboboxes, check only radio/checkboxes, upload_file only file inputs, click/submit only buttons or links.
            Before clicking Next, Review, Continue, or Submit, answer every visible required question, empty required field, required select, combobox, radio, or checkbox.
            Never choose a random non-placeholder option for an unknown required select. Use checkpoint if no safe source exists.
            For yes/no technical questions, answer from the candidate profile. If a technology is not present in profile skills/summary, choose No/0 rather than claiming experience.
            Prefer one safe atomic action. If unsure, use checkpoint.
            Allowed actionType values: click, fill, select, check, upload_file, scroll, wait, submit, checkpoint, done.
            Use checkpoint for captcha, 2FA, payment, legal/sensitive questions without explicit source, ambiguity, or low confidence.
            """;

        var prompt = $$"""
            Objective: {{request.Objective}}
            Attempt: {{request.Attempt}}
            Page state: {{request.PageState}}
            Job context: {{request.JobContext}}
            Previous failure: {{request.PreviousFailure}}
            Short decision memory: {{request.DecisionMemory}}

            Candidate profile:
            {{JsonSerializer.Serialize(profile, jsonOptions)}}

            Reusable answers:
            {{JsonSerializer.Serialize(answers.Select(a => new { a.QuestionKey, a.QuestionText, a.AnswerValue, a.Category }), jsonOptions)}}

            Current observation JSON:
            {{request.ObservationJson}}

            Return JSON:
            {
              "actionType": "fill|click|select|check|upload_file|scroll|wait|submit|checkpoint|done",
              "elementId": "observed element id or empty",
              "value": "value to type/select or empty",
              "reason": "short reason",
              "confidence": 0.0,
              "requiresCheckpoint": false
            }
            """;

        return new BrowserDecisionPrompt(system, prompt, BrowserActionJsonSchema);
    }

    public static BrowserDecisionValidation ValidateBrowserActionPlan(BrowserActionPlanResponse plan, string observationJson)
    {
        var normalized = ClampConfidence(plan);
        if (!IsAllowedActionType(normalized.ActionType))
        {
            var detail = $"AI planner returned an unsupported action type: '{normalized.ActionType}'.";
            return new BrowserDecisionValidation(
                new BrowserActionPlanResponse("checkpoint", "", "", detail, 0.2m, true),
                "llm.invalid_action_type",
                detail);
        }

        if (normalized.ActionType == "upload_file" && !string.IsNullOrWhiteSpace(normalized.Value))
            normalized = normalized with { Value = "", Reason = $"{normalized.Reason} Resume path supplied by local agent, not by LLM." };

        if (normalized.RequiresCheckpoint || normalized.ActionType is "checkpoint" or "done" or "scroll" or "wait")
            return new BrowserDecisionValidation(normalized, null, null);

        var observed = ReadObservedElements(observationJson);
        if (string.IsNullOrWhiteSpace(normalized.ElementId) || !observed.ContainsKey(normalized.ElementId))
        {
            var detail = $"AI planner chose an element that is not present in the current observation: '{normalized.ElementId}'.";
            return new BrowserDecisionValidation(
                new BrowserActionPlanResponse("checkpoint", "", "", detail, 0.25m, true),
                "llm.invalid_element",
                detail);
        }

        if (!ActionMatchesObservedElement(normalized, observed[normalized.ElementId], out var typeDetail))
        {
            return new BrowserDecisionValidation(
                new BrowserActionPlanResponse("checkpoint", "", "", typeDetail, 0.25m, true),
                "llm.invalid_target_type",
                typeDetail);
        }

        if (IsNavigationAction(normalized, observed[normalized.ElementId]) &&
            HasBlockingRequiredIssue(observationJson, observed.Values, out var blockingDetail))
        {
            var detail = $"AI planner tried to advance while required fields are still invalid: {blockingDetail}";
            return new BrowserDecisionValidation(
                new BrowserActionPlanResponse("checkpoint", "", "", detail, 0.25m, true),
                "llm.blocked_by_required_fields",
                detail);
        }

        return new BrowserDecisionValidation(normalized, null, null);
    }

    public static string DescribeParseFailure(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? "AI planner returned an empty response."
            : $"AI planner did not return valid JSON. Raw length={raw.Length}.";

    private static BrowserActionPlanResponse ClampConfidence(BrowserActionPlanResponse plan) =>
        plan with { Confidence = Math.Clamp(plan.Confidence, 0m, 1m) };

    private static Dictionary<string, ObservedElement> ReadObservedElements(string observationJson)
    {
        var elementsById = new Dictionary<string, ObservedElement>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(observationJson);
            if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
                return elementsById;

            foreach (var element in elements.EnumerateArray())
            {
                if (!element.TryGetProperty("elementId", out var id) || id.ValueKind != JsonValueKind.String) continue;
                var value = id.GetString();
                if (string.IsNullOrWhiteSpace(value)) continue;
                elementsById[value] = new ObservedElement(
                    value,
                    ReadString(element, "tag"),
                    ReadString(element, "type"),
                    ReadString(element, "role"),
                    ReadString(element, "label"),
                    ReadString(element, "text"),
                    ReadString(element, "value"),
                    ReadBool(element, "required"),
                    ReadBool(element, "disabled"),
                    ReadBool(element, "checked"));
            }
        }
        catch
        {
            return elementsById;
        }

        return elementsById;
    }

    private static bool IsNavigationAction(BrowserActionPlanResponse plan, ObservedElement target)
    {
        if (plan.ActionType == "submit") return true;
        if (plan.ActionType != "click") return false;

        var text = Normalize($"{target.Label} {target.Text} {plan.Reason}");
        if (ContainsAny(text, ["back", "voltar", "cancel", "cancelar"])) return false;
        return ContainsAny(text, ["next", "continue", "review", "submit", "avancar", "avançar", "continuar", "revisar", "revise", "enviar"]);
    }

    private static bool ActionMatchesObservedElement(BrowserActionPlanResponse plan, ObservedElement target, out string detail)
    {
        var valid = plan.ActionType switch
        {
            "fill" => IsTextEntry(target),
            "select" => IsSelect(target),
            "check" => IsChoice(target),
            "upload_file" => target.Type.Equals("file", StringComparison.OrdinalIgnoreCase),
            "click" or "submit" => IsButtonLike(target),
            _ => false
        };

        if (valid)
        {
            detail = "";
            return true;
        }

        detail = $"AI planner action '{plan.ActionType}' is incompatible with observed element '{target.ElementId}' ({target.Tag}/{target.Type}/{target.Role}).";
        return false;
    }

    private static bool HasBlockingRequiredIssue(string observationJson, IEnumerable<ObservedElement> elements, out string detail)
    {
        var visibleError = ReadVisibleValidationError(observationJson);
        if (!string.IsNullOrWhiteSpace(visibleError))
        {
            detail = $"visible validation error '{visibleError}'";
            return true;
        }

        foreach (var element in elements)
        {
            if (element.Disabled || !element.Required || !IsField(element)) continue;

            if (IsChoice(element) && !element.Checked)
            {
                detail = $"unanswered choice '{element.Label}'";
                return true;
            }

            if (IsSelect(element) && LooksLikePlaceholder($"{element.Value} {element.Text}"))
            {
                detail = $"empty select/combobox '{element.Label}'";
                return true;
            }

            if (IsTextEntry(element) && string.IsNullOrWhiteSpace(element.Value))
            {
                detail = $"empty text field '{element.Label}'";
                return true;
            }

            if (element.Type.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                !(LooksLikeResumeField(element) && ResumeAlreadySelected(observationJson)))
            {
                detail = $"unhandled required file input '{element.Label}'";
                return true;
            }
        }

        detail = "";
        return false;
    }

    private static string ReadVisibleValidationError(string observationJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(observationJson);
            if (!doc.RootElement.TryGetProperty("visibleText", out var visibleTextElement) || visibleTextElement.ValueKind != JsonValueKind.String)
                return "";

            var text = visibleTextElement.GetString() ?? "";
            return text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => ContainsAny(Normalize(line), [
                    "insira uma resposta valida", "enter a valid answer", "campo obrigatorio",
                    "required", "please enter", "please select"
                ])) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool ResumeAlreadySelected(string observationJson)
    {
        var visibleText = ReadVisibleText(observationJson);
        return ContainsAny(Normalize(visibleText), [
            ".pdf", ".doc", ".docx", "curriculo nicolas", "curriculo", "resume",
            "ultima utilizacao", "desmarcar selecao"
        ]);
    }

    private static bool LooksLikeResumeField(ObservedElement element) =>
        ContainsAny(Normalize($"{element.Label} {element.Text}"), ["curriculo", "resume", "cv"]);

    private static string ReadVisibleText(string observationJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(observationJson);
            return doc.RootElement.TryGetProperty("visibleText", out var visibleTextElement) && visibleTextElement.ValueKind == JsonValueKind.String
                ? visibleTextElement.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    private static bool IsField(ObservedElement element) =>
        element.Tag is "input" or "textarea" or "select" ||
        element.Role is "textbox" or "combobox" or "radio" or "checkbox" ||
        LooksLikePlaceholder($"{element.Label} {element.Text}");

    private static bool IsSelect(ObservedElement element) =>
        element.Tag == "select" ||
        element.Role == "combobox" ||
        LooksLikePlaceholder($"{element.Label} {element.Text} {element.Value}");

    private static bool IsChoice(ObservedElement element) =>
        element.Type is "radio" or "checkbox" || element.Role is "radio" or "checkbox";

    private static bool IsTextEntry(ObservedElement element) =>
        element.Tag == "textarea" ||
        (element.Tag == "input" && element.Type is not ("radio" or "checkbox" or "file" or "submit" or "button"));

    private static bool IsButtonLike(ObservedElement element) =>
        element.Tag is "button" or "a" || element.Role == "button";

    private static bool IsAllowedActionType(string actionType) =>
        actionType is "click" or "fill" or "select" or "check" or "upload_file"
            or "scroll" or "wait" or "submit"
            or "checkpoint" or "done";

    private static bool LooksLikePlaceholder(string text) =>
        string.IsNullOrWhiteSpace(text) ||
        ContainsAny(Normalize(text), ["selecionar opcao", "selecionar uma opcao", "select an option", "choose", "escolha", "--"]);

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool ContainsAny(string normalizedText, IEnumerable<string> values) =>
        values.Any(value => normalizedText.Contains(Normalize(value), StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var withoutDiacritics = new string(normalized.Where(ch =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return withoutDiacritics.ToLowerInvariant();
    }

    private sealed record ObservedElement(
        string ElementId,
        string Tag,
        string Type,
        string Role,
        string Label,
        string Text,
        string Value,
        bool Required,
        bool Disabled,
        bool Checked);
}
