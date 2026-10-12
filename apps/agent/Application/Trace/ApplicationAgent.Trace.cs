using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Harness.Audit;
using JobPilot.Agent.Infrastructure;
using JobPilot.Agent.Platforms;
using JobPilot.Agent.Platforms.Inhire;
using JobPilot.Agent.Platforms.LinkedIn;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public sealed partial class ApplicationAgent
{
    private ObservationFrame CreateObservationFrame(PageObservation observation, IPlatformModule platform, FormStateMachine stateMachine) =>
        new(
            observation,
            platform.Name,
            platform.GetType().Name,
            stateMachine.CurrentPhase.ToString(),
            observation.Fingerprint(),
            DateTimeOffset.UtcNow);

    private DecisionFrame CreateDecisionFrame(AgentTaskResponse task, string jobContext, ObservationFrame observationFrame, FormAuditResult audit, ApplicationSurfaceOpenResult applicationSurface) =>
        new(
            new AgentObjective(
                "apply",
                "Fill the job application form safely. Submit only when policy and guardrails allow it.",
                _options.PolicyMode.Equals("aggressive", StringComparison.OrdinalIgnoreCase),
                task.Lead.Url,
                _options.PolicyMode,
                applicationSurface.SurfaceKind,
                string.IsNullOrWhiteSpace(applicationSurface.FinalUrl) ? observationFrame.Observation.Url : applicationSurface.FinalUrl,
                applicationSurface.SuggestedPlatform),
            observationFrame,
            audit,
            jobContext,
            _options.PolicyMode);

    private async Task<Guid?> RecordCheckpointTraceAsync(
        Guid? runId,
        Guid applicationJobId,
        int step,
        IBrowserDriver driver,
        PageObservation observation,
        DecisionFrame frame,
        ObservedContentSafetyReport contentSafety,
        ActionProposal proposal,
        string failureCode,
        string suggestedUserAction,
        DateTimeOffset startedAt)
    {
        var risk = _riskClassifier.Classify(frame, proposal, contentSafety);
        var verdict = BrowserActionGuard.Evaluate(frame, proposal, risk);
        var outcome = new ActionOutcome(
            false,
            "checkpoint",
            proposal.Reason,
            frame.Observation.Fingerprint,
            frame.Observation.Fingerprint,
            false,
            failureCode,
            new FailureRecord(failureCode, "human_required", proposal.Reason, false),
            true);
        var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, proposal.Reason, false, suggestedUserAction, "reobserve");
        var evidence = await CaptureStepEvidenceAsync(driver, observation, applicationJobId, step, failureCode);
        return await RecordTraceStepAsync(runId, applicationJobId, step, frame, contentSafety, proposal, risk, verdict, new { skipped = true, reason = failureCode }, outcome, recovery, evidence, startedAt);
    }

    private async Task<Guid?> RecordTraceStepAsync(
        Guid? runId,
        Guid applicationJobId,
        int step,
        DecisionFrame frame,
        ObservedContentSafetyReport contentSafety,
        ActionProposal proposal,
        ActionRisk risk,
        ActionVerdict verdict,
        object execution,
        ActionOutcome outcome,
        RecoveryDecision recovery,
        string evidencePath,
        DateTimeOffset startedAt,
        ObservationFrame? afterFrame = null)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var effectiveAfter = afterFrame ?? frame.Observation;
        var request = new AgentTraceStepRequest(
            runId,
            applicationJobId,
            step,
            frame.Objective.Kind,
            RedactJson(frame.Objective),
            frame.Observation.Platform,
            frame.Observation.Module,
            frame.Observation.Phase,
            frame.Observation.Fingerprint,
            effectiveAfter.Fingerprint,
            proposal.Planner,
            RedactJson(proposal),
            RedactJson(risk),
            RedactJson(new { verdict.Allowed, verdict.Verdict, verdict.Reason, verdict.Guardrails, verdict.RequiresCheckpoint }),
            RedactJson(execution),
            RedactJson(outcome),
            RedactJson(recovery),
            RedactJson(contentSafety),
            _traceRedactor.RedactText(evidencePath, TraceRedactionScope.TracePersistence),
            startedAt,
            completedAt);

        return await _client.TraceStepAsync(request);
    }

    private string RedactJson(object value) =>
        _traceRedactor.RedactObject(value, TraceRedactionScope.TracePersistence);

}
