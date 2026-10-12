using JobPilot.Agent.Domain;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public sealed class PostActionValidator
{
    private readonly FieldValidator _fieldValidator;
    private readonly ErrorClassifier _errorClassifier;
    private readonly LocalAnswerResolver _resolver;

    public PostActionValidator(FieldValidator fieldValidator, ErrorClassifier errorClassifier, LocalAnswerResolver resolver)
    {
        _fieldValidator = fieldValidator;
        _errorClassifier = errorClassifier;
        _resolver = resolver;
    }

    public async Task<ActionExecutionResult> ValidateFillAsync(
        IPage page,
        ILocator locator,
        ElementCandidate element,
        string originalValue,
        SessionContext session)
    {
        await page.WaitForTimeoutAsync(400);

        var currentValue = await locator.InputValueAsync();
        var valueToValidate = string.IsNullOrWhiteSpace(currentValue) ? originalValue : currentValue;
        var validation = _fieldValidator.Validate(element, valueToValidate);
        if (validation.IsValid)
            return ActionExecutionResult.Ok("Validated.");

        var category = _errorClassifier.Classify("fill", validation.ErrorCode, validation.ErrorMessage, session.RetryCount);

        if (category == ErrorCategory.HumanRequired)
        {
            return ActionExecutionResult.Fail(
                $"validation_{validation.ErrorCode}",
                $"Field validation failed (human required): {validation.ErrorMessage}");
        }

        if (category == ErrorCategory.Retriable)
        {
            session.RetryCount++;
            await locator.FillAsync("");

            var alternative = _resolver.ResolveAlternative(element, session, session.RetryCount);
            if (alternative?.Value is not null)
            {
                await locator.FillAsync(alternative.Value);
                await page.WaitForTimeoutAsync(400);

                var retryValue = await locator.InputValueAsync();
                var retryValidation = _fieldValidator.Validate(element, retryValue);
                if (retryValidation.IsValid)
                {
                    session.RetryCount = 0;
                    return ActionExecutionResult.Ok($"Self-healed: {alternative.Reason}");
                }
            }

            if (session.RetryCount >= 3)
            {
                return ActionExecutionResult.Fail(
                    $"validation_exhausted_{validation.ErrorCode}",
                    $"Field validation failed after {session.RetryCount} retries: {validation.ErrorMessage}");
            }
        }

        return ActionExecutionResult.Fail(
            $"validation_{validation.ErrorCode}",
            $"Field validation failed: {validation.ErrorMessage}");
    }

    public async Task<ActionExecutionResult> ValidateSelectionAsync(
        IPage page,
        ILocator locator,
        ElementCandidate element,
        string expectedValue)
    {
        await page.WaitForTimeoutAsync(500);

        try
        {
            var currentValue = await locator.EvaluateAsync<string>("el => el.value || el.textContent || ''");
            var visibleText = await PageObserver.GetVisibleTextAsync(page);
            var visibleErrors = ExtractVisibleValidationHints(visibleText);
            if (!string.IsNullOrWhiteSpace(visibleErrors))
                return ActionExecutionResult.Fail("selection_validation_error", $"Selection left visible validation hints on the page: {visibleErrors}");

            if (!string.IsNullOrWhiteSpace(expectedValue) &&
                currentValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
                return ActionExecutionResult.Ok("Selection validated.");

            if (element.Required && FieldValuePolicy.LooksLikePlaceholderOption(currentValue))
                return ActionExecutionResult.Fail("selection_still_placeholder", "Required select/combobox is still on a placeholder value.");

            if (!element.Required || !string.IsNullOrWhiteSpace(currentValue))
                return ActionExecutionResult.Ok("Selection changed.");

            return ActionExecutionResult.Fail("selection_not_applied", "Required selection did not produce a visible value.");
        }
        catch
        {
            if (element.Required)
                return ActionExecutionResult.Fail("selection_validation_unavailable", "Required selection could not be validated after action.");

            return ActionExecutionResult.Ok("Optional selection executed; validation skipped because control changed.");
        }
    }

    public async Task<ActionExecutionResult> ValidateNavigationActionAsync(
        IPage page,
        PageObservation before,
        BrowserAction action)
    {
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 5000 }).ContinueWith(_ => { });
        await page.WaitForTimeoutAsync(900);

        var after = await PageObserver.ObserveAsync(page);
        if (after.Url != before.Url || after.Fingerprint() != before.Fingerprint())
            return ActionExecutionResult.Ok("Page changed after navigation action.");

        var visibleErrors = ExtractVisibleValidationHints(after.VisibleText);
        if (!string.IsNullOrWhiteSpace(visibleErrors))
            return ActionExecutionResult.Fail("validation_failed", $"Page did not advance and shows validation hints: {visibleErrors}");

        if (action.Type == "submit")
            return ActionExecutionResult.Fail("submit_no_state_change", "Submit action did not change the page or show confirmation.");

        var target = before.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (action.Type == "click" && target is not null &&
            (PageStateClassifier.LooksLikeNextElement(target) || PageStateClassifier.LooksLikeSubmitElement(target) || PageStateClassifier.LooksLikeApply(target.SearchText)))
        {
            return ActionExecutionResult.Fail("navigation_no_state_change", "Navigation/review/apply click did not change the page state.");
        }

        return ActionExecutionResult.Ok("Clicked; page state stayed stable.");
    }

    public ActionOutcome ValidateOutcome(
        ObservationFrame before,
        ApprovedAction approvedAction,
        ObservationFrame after,
        ActionExecutionResult execution,
        SessionContext session)
    {
        if (execution.Success)
            return ActionOutcome.FromExecution(execution, before, after);

        var category = _errorClassifier.Classify(
            approvedAction.Proposal.Type,
            execution.CheckpointCode,
            execution.Message,
            session.RetryCount);
        var failure = new FailureRecord(
            execution.CheckpointCode ?? "action_failed",
            category.ToString().ToLowerInvariant(),
            execution.Message,
            category == ErrorCategory.Retriable);

        return ActionOutcome.FromExecution(execution, before, after, failure);
    }

    private static string ExtractVisibleValidationHints(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = lines.Where(line => AgentText.ContainsAny(line, [
                "obrigatorio", "obrigatorio", "required", "invalid", "invalido", "erro",
                "preencha", "selecione", "campo obrigatorio", "must", "please enter", "please select",
                "insira uma resposta valida", "enter a valid answer", "select an option", "selecionar opcao"
            ]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        return string.Join(" | ", matches);
    }
}
