using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

public enum RiskLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RecoveryMode
{
    None,
    Retry,
    Reobserve,
    Scroll,
    Remap,
    Checkpoint,
    Abort
}

public sealed record AgentObjective(
    string Kind,
    string Description,
    bool AllowSubmit,
    string TargetUrl = "",
    string PolicyMode = "conservative",
    string SurfaceKind = "",
    string SurfaceUrl = "",
    string SuggestedPlatform = "");

public sealed record ObservationFrame(
    PageObservation Observation,
    string Platform,
    string Module,
    string Phase,
    string Fingerprint,
    DateTimeOffset ObservedAt);

public sealed record DecisionFrame(
    AgentObjective Objective,
    ObservationFrame Observation,
    FormAuditResult Audit,
    string JobContext,
    string PlannerPolicy,
    string PreviousFailure = "");

public sealed record ActionProposal(
    string Type,
    string ElementId,
    string Value,
    string Label,
    string Reason,
    decimal Confidence,
    string Planner,
    string ExpectedEffect,
    bool RequiresCheckpoint = false)
{
    public static ActionProposal FromAction(BrowserAction action, string planner, string expectedEffect = "", bool requiresCheckpoint = false) =>
        new(
            action.Type,
            action.ElementId,
            action.Value,
            action.Label,
            action.Reason,
            action.Confidence,
            planner,
            string.IsNullOrWhiteSpace(expectedEffect) ? InferExpectedEffect(action) : expectedEffect,
            requiresCheckpoint || action.Type == "checkpoint");

    public BrowserAction ToBrowserAction() =>
        new(Type, ElementId, Value, Label, Reason, Confidence);

    private static string InferExpectedEffect(BrowserAction action) => action.Type switch
    {
        "fill" => "field_value_changes",
        "select" => "selection_changes",
        "check" => "choice_checked",
        "upload_file" => "file_attached",
        "click" => "page_or_surface_changes",
        "submit" => "application_submitted_or_confirmation_visible",
        "scroll" => "new_content_visible",
        "wait" => "page_stabilizes",
        "done" => "runtime_marks_objective_done",
        "checkpoint" => "human_review_requested",
        _ => "unknown"
    };
}

public sealed record RiskBudget(
    RiskLevel MaxAutoRisk,
    bool AllowSubmit,
    bool AllowSensitiveWithoutSource,
    bool AllowGenericSubmit)
{
    public static RiskBudget FromPolicy(AgentPolicy? policy, string platform)
    {
        var mode = policy?.AutoSubmitMode ?? "conservative";
        var allowSubmit = mode.Equals("aggressive", StringComparison.OrdinalIgnoreCase);
        var maxRisk = allowSubmit ? RiskLevel.High : RiskLevel.Medium;
        return new RiskBudget(
            maxRisk,
            allowSubmit,
            false,
            allowSubmit && !platform.Equals("Generic", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record ActionRisk(
    RiskLevel Level,
    string[] Reasons,
    bool IsSubmitBoundary,
    bool RequiresCheckpoint,
    RiskBudget Budget)
{
    public static ActionRisk Low(RiskBudget budget, params string[] reasons) =>
        new(RiskLevel.Low, reasons.Length == 0 ? ["low_risk_action"] : reasons, false, false, budget);
}

public sealed record ApprovedAction(
    ActionProposal Proposal,
    ActionRisk Risk,
    string ApprovedBy,
    DateTimeOffset ApprovedAt)
{
    public BrowserAction ToBrowserAction() => Proposal.ToBrowserAction();
}

public sealed record ActionVerdict(
    bool Allowed,
    string Verdict,
    string Reason,
    ActionRisk Risk,
    ApprovedAction? ApprovedAction,
    string[] Guardrails,
    bool RequiresCheckpoint);

public sealed record FailureRecord(
    string Code,
    string FailureClass,
    string Detail,
    bool Retriable);

public sealed record ActionOutcome(
    bool Success,
    string Status,
    string Message,
    string BeforeFingerprint,
    string AfterFingerprint,
    bool StateChanged,
    string? FailureCode,
    FailureRecord? Failure,
    bool RequiresCheckpoint)
{
    public static ActionOutcome FromExecution(
        ActionExecutionResult execution,
        ObservationFrame before,
        ObservationFrame after,
        FailureRecord? failure = null)
    {
        var stateChanged = before.Fingerprint != after.Fingerprint;
        var failureRecord = execution.Success
            ? null
            : failure ?? new FailureRecord(execution.CheckpointCode ?? "action_failed", "unknown", execution.Message, false);

        return new ActionOutcome(
            execution.Success,
            execution.Success ? (stateChanged ? "validated_changed" : "validated_stable") : "failed",
            execution.Message,
            before.Fingerprint,
            after.Fingerprint,
            stateChanged,
            execution.CheckpointCode,
            failureRecord,
            !execution.Success && failureRecord?.Retriable != true);
    }
}

public sealed record RecoveryDecision(
    RecoveryMode Mode,
    string Reason,
    bool ContinueLoop,
    string SuggestedUserAction = "",
    string ResumeMode = "reobserve");

public sealed record ObservedContentSafetySignal(string Code, string Severity, string Evidence);

public sealed record ObservedContentSafetyReport(
    bool HasSignals,
    bool HasPromptInjectionSignals,
    RiskLevel RiskLevel,
    string Summary,
    IReadOnlyList<ObservedContentSafetySignal> Signals)
{
    public static ObservedContentSafetyReport Clean { get; } =
        new(false, false, RiskLevel.None, "No suspicious observed content signals.", []);
}

public enum TraceRedactionScope
{
    TracePersistence,
    Prompt,
    Display
}

public interface IObservedContentSafetyScanner
{
    ObservedContentSafetyReport Scan(ObservationFrame observation);
}

public interface ITraceRedactor
{
    string RedactText(string text, TraceRedactionScope scope);
    string RedactJson(string json, TraceRedactionScope scope);
}

public sealed class ObservedContentSafetyScanner : IObservedContentSafetyScanner
{
    private static readonly (string Code, string Severity, string[] Needles)[] Rules =
    [
        ("prompt_injection_ignore_instructions", "high", ["ignore previous instructions", "ignore all previous", "ignore the system", "desconsidere instrucoes", "ignore as instrucoes"]),
        ("prompt_injection_policy_change", "high", ["change your objective", "alter your policy", "bypass policy", "override guardrails", "desative as regras"]),
        ("credential_exfiltration_instruction", "critical", ["send your password", "send api key", "bearer token", "cookie:", "cole sua senha", "informe sua senha"]),
        ("out_of_objective_instruction", "medium", ["apply to a different job", "open another website", "click the advertisement", "transfer money", "acesse outro site"]),
        ("llm_roleplay_instruction", "medium", ["you are now", "developer message", "system message", "act as", "aja como"])
    ];

    public ObservedContentSafetyReport Scan(ObservationFrame observation)
    {
        var text = $"{observation.Observation.Title}\n{observation.Observation.VisibleText}\n{string.Join('\n', observation.Observation.Elements.Select(e => $"{e.Label} {e.Text} {e.Value}"))}";
        if (string.IsNullOrWhiteSpace(text))
            return ObservedContentSafetyReport.Clean;

        var normalized = AgentText.RemoveDiacritics(text).ToLowerInvariant();
        var signals = new List<ObservedContentSafetySignal>();
        foreach (var (code, severity, needles) in Rules)
        {
            var match = needles.FirstOrDefault(needle => normalized.Contains(AgentText.RemoveDiacritics(needle).ToLowerInvariant()));
            if (match is null) continue;
            signals.Add(new ObservedContentSafetySignal(code, severity, ExtractEvidence(text, match)));
        }

        if (signals.Count == 0)
            return ObservedContentSafetyReport.Clean;

        var risk = signals.Any(s => s.Severity == "critical")
            ? RiskLevel.Critical
            : signals.Any(s => s.Severity == "high")
                ? RiskLevel.High
                : RiskLevel.Medium;

        return new ObservedContentSafetyReport(
            true,
            signals.Any(s => s.Code.StartsWith("prompt_injection", StringComparison.OrdinalIgnoreCase) || s.Code.Contains("credential", StringComparison.OrdinalIgnoreCase)),
            risk,
            $"{signals.Count} suspicious observed content signal(s) detected. Page text remains untrusted data.",
            signals);
    }

    private static string ExtractEvidence(string text, string needle)
    {
        var index = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return needle;
        var start = Math.Max(0, index - 48);
        var length = Math.Min(text.Length - start, needle.Length + 96);
        return Regex.Replace(text.Substring(start, length), "\\s+", " ").Trim();
    }
}

public sealed class TraceRedactor : ITraceRedactor
{
    private static readonly Regex SecretAssignment = new(
        @"(?i)(""?(password|senha|token|api[_-]?key|secret|cookie|authorization|bearer)""?\s*[:=]\s*)("".*?""|'[^']*'|[^\s,;}]+)",
        RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(@"(?i)bearer\s+[a-z0-9._\-+/=]{12,}", RegexOptions.Compiled);
    private static readonly Regex LongSecret = new(@"(?i)\b(sk-[a-z0-9_\-]{12,}|(?=[a-z0-9_\-]{32,}\b)(?=[a-z0-9_\-]*[a-z])(?=[a-z0-9_\-]*\d)[a-z0-9_\-]{32,})\b", RegexOptions.Compiled);
    private static readonly Regex Cpf = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);

    public string RedactJson(string json, TraceRedactionScope scope)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        return RedactText(json, scope);
    }

    public string RedactText(string text, TraceRedactionScope scope)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var redacted = SecretAssignment.Replace(text, match => $"{match.Groups[1].Value}\"[redacted-secret]\"");
        redacted = BearerToken.Replace(redacted, "Bearer [redacted-secret]");
        redacted = LongSecret.Replace(redacted, "[redacted-secret]");
        redacted = Cpf.Replace(redacted, "[redacted-cpf]");
        return redacted;
    }

    public string RedactObject(object value, TraceRedactionScope scope = TraceRedactionScope.TracePersistence)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(value, options);
        return RedactJson(json, scope);
    }
}
