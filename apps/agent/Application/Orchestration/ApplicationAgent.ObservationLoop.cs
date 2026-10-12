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
    private async Task<bool> RunApplicationLoopAsync(IPage? page, AgentTaskResponse task, IPlatformModule platform, ApplicationSurfaceOpenResult applicationSurface)
    {
        var jobContext = $"{task.Lead.Title} at {task.Lead.Company}. {task.Lead.Location}. {task.Lead.Url}";
        if (!string.IsNullOrWhiteSpace(applicationSurface.SurfaceKind))
        {
            var surfaceUrl = string.IsNullOrWhiteSpace(applicationSurface.FinalUrl) ? page?.Url ?? "" : applicationSurface.FinalUrl;
            jobContext += $" Application surface: {applicationSurface.SurfaceKind}; finalUrl={surfaceUrl}; suggestedPlatform={applicationSurface.SuggestedPlatform}.";
        }
        var seenStates = new Dictionary<string, int>();
        var session = new SessionContext();
        var stateMachine = new FormStateMachine();
        var runId = await _client.StartRunAsync(task.Task.Id, "ApplicationAgent", "application", _options.PolicyMode);
        IBrowserDriver driver = page is null
            ? new BrowserExtensionDriver(new BrowserExtensionHttpTransport(_client))
            : CreateBrowserDriver(page);

        try
        {
            for (var step = 0; step < 18; step++)
            {
                if (driver is not BrowserExtensionDriver && page is not null && await EnsurePlatformReadyAsync(platform, page, task.Task.Id, "runtime"))
                {
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                var stepStartedAt = DateTimeOffset.UtcNow;
                var observation = await driver.ObserveAsync();
                stateMachine.Transition(observation, null);
                var observationFrame = CreateObservationFrame(observation, platform, stateMachine);
                var audit = _formAuditor.Audit(observation, task);
                var decisionFrame = CreateDecisionFrame(task, jobContext, observationFrame, audit, applicationSurface);
                var contentSafety = _contentSafetyScanner.Scan(observationFrame);
                await _client.EventAsync(task.Task.Id, "runtime.observe",
                    $"step={step}; phase={stateMachine.CurrentPhase}; ats={stateMachine.DetectedAts}; url={observation.Url}; elements={observation.Elements.Count}; safety={contentSafety.RiskLevel}");

                // Confirmacao
                if (LlmDecisionEngine.IsConfirmation(observation) || stateMachine.CurrentPhase == FormPhase.Confirmation)
                {
                    var terminalProposal = ActionProposal.FromAction(new BrowserAction("done", "", "", "", "Confirmation page detected.", 0.95m), "completion_detector");
                    var risk = _riskClassifier.Classify(decisionFrame, terminalProposal, contentSafety);
                    var verdict = BrowserActionGuard.Evaluate(decisionFrame, terminalProposal, risk);
                    var outcome = new ActionOutcome(true, "applied", "Confirmation page detected.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, null, null, false);
                    var recovery = new RecoveryDecision(RecoveryMode.None, "Application is confirmed.", false);
                    await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, terminalProposal, risk, verdict, new { skipped = true, reason = "confirmation_detected" }, outcome, recovery, "", stepStartedAt);
                    await RememberConfirmationLearningAsync(observation, task.Task.Id);
                    await _client.SetStateAsync(task.Task.Id, "applied", "confirmation_detected");
                    await _client.EventAsync(task.Task.Id, "job.applied", observation.Url);
                    await _client.CompleteRunAsync(runId, "applied");
                    return true;
                }

                // Bloqueio
                if (LlmDecisionEngine.IsBlockedState(observation) || stateMachine.CurrentPhase == FormPhase.Blocked)
                {
                    var terminalProposal = ActionProposal.FromAction(BrowserAction.Checkpoint("", "", "Security, captcha, or login challenge detected."), "block_detector", requiresCheckpoint: true);
                    var traceStepId = await RecordCheckpointTraceAsync(runId, task.Task.Id, step, driver, observation, decisionFrame, contentSafety, terminalProposal, "captcha_or_security", "Resolve the security challenge in the browser before resuming.", stepStartedAt);
                    await CheckpointAsync(page, task.Task.Id, "captcha_or_security", "runtime", "", "Security, captcha, or login challenge detected.", runId, traceStepId, "Resolve the security challenge in the browser before resuming.", "reobserve");
                    await _client.SetStateAsync(task.Task.Id, "needs_review", "security_checkpoint");
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                // Stuck detection melhorado (state machine + fingerprint)
                var fingerprint = observation.Fingerprint();
                seenStates[fingerprint] = seenStates.GetValueOrDefault(fingerprint) + 1;
                if (seenStates[fingerprint] >= 4 || stateMachine.IsStuck)
                {
                    var detail = $"The agent got stuck. Phase={stateMachine.CurrentPhase}, Step={stateMachine.CurrentStep}, Streak={stateMachine.IsStuck}";
                    var terminalProposal = ActionProposal.FromAction(BrowserAction.Checkpoint("", "", detail), "stuck_detector", requiresCheckpoint: true);
                    var traceStepId = await RecordCheckpointTraceAsync(runId, task.Task.Id, step, driver, observation, decisionFrame, contentSafety, terminalProposal, "stuck_detection", "Inspect the current page and resume after a fresh observation.", stepStartedAt);
                    await CheckpointAsync(page, task.Task.Id, "stuck_detection", "runtime", "", detail, runId, traceStepId, "Inspect the current page and resume after a fresh observation.", "reobserve");
                    await _client.SetStateAsync(task.Task.Id, "needs_review", "stuck_detection");
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                var proposal = await _decision.ProposeAsync(decisionFrame, task, session)
                    ?? ActionProposal.FromAction(BrowserAction.Checkpoint("", "", "No safe action was available."), "planner", requiresCheckpoint: true);
                var action = proposal.ToBrowserAction();

                if (proposal.Type == "checkpoint")
                {
                    var traceStepId = await RecordCheckpointTraceAsync(runId, task.Task.Id, step, driver, observation, decisionFrame, contentSafety, proposal, "checkpoint_required", "Review the requested checkpoint and resume after reobserving the page.", stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ApplicationAgent", LlmDecisionEngine.ClassifyPageState(observation), "checkpoint", proposal.Label, proposal.Reason, "", proposal.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "checkpoint_required", "runtime", proposal.Label, proposal.Reason, runId, traceStepId, "Review the requested checkpoint and resume after reobserving the page.", "reobserve");
                    await _client.SetStateAsync(task.Task.Id, "needs_review", "checkpoint_required");
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                if (action.Type == "done")
                {
                    var risk = _riskClassifier.Classify(decisionFrame, proposal, contentSafety);
                    var verdict = BrowserActionGuard.Evaluate(decisionFrame, proposal, risk);
                    var outcome = new ActionOutcome(true, "applied", proposal.Reason, observationFrame.Fingerprint, observationFrame.Fingerprint, false, null, null, false);
                    var recovery = new RecoveryDecision(RecoveryMode.None, proposal.Reason, false);
                    await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, risk, verdict, new { skipped = true, reason = "planner_done" }, outcome, recovery, "", stepStartedAt);
                    await _client.SetStateAsync(task.Task.Id, "applied", action.Reason);
                    await _client.EventAsync(task.Task.Id, "job.applied", observation.Url);
                    await _client.CompleteRunAsync(runId, "applied");
                    return true;
                }

                var appliedLearning = TryApplyLearningSuggestion(observation, action);
                if (appliedLearning is not null)
                {
                    action = action with
                    {
                        Reason = $"{action.Reason} LearningSuggestion={appliedLearning.PatternKey}",
                        Confidence = Math.Min(0.98m, Math.Max(action.Confidence, action.Confidence + 0.03m))
                    };
                }

                action = SkipCompletedFieldAction(observation, action, session) ?? action;
                proposal = ActionProposal.FromAction(action, proposal.Planner, proposal.ExpectedEffect, proposal.RequiresCheckpoint);
                var actionRisk = _riskClassifier.Classify(decisionFrame, proposal, contentSafety);
                var actionVerdict = BrowserActionGuard.Evaluate(decisionFrame, proposal, actionRisk);

                if (IsOutOfApplySurfaceAction(action, observation, applicationSurface))
                {
                    var evidence = await CaptureStepEvidenceAsync(driver, observation, task.Task.Id, step, "out-of-surface-action");
                    var outcome = new ActionOutcome(false, "needs_review", "Action targets LinkedIn page chrome instead of the application surface.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, "out_of_surface_action", new FailureRecord("out_of_surface_action", "guard", action.Reason, false), true);
                    var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, outcome.Message, false, "Review the Easy Apply modal before resuming.", "reobserve");
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "out_of_surface_action" }, outcome, recovery, evidence, stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "GuardAgent", LlmDecisionEngine.ClassifyPageState(observation), "out_of_surface_action", action.Label, action.Reason, evidence, action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "out_of_surface_action", "runtime", action.Label,
                        $"Blocked action outside application surface. job={task.Lead.Title}; company={task.Lead.Company}; url={observation.Url}; proposed={action.Type} {action.Label}; reason={action.Reason}",
                        runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "needs_review", "out_of_surface_action");
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                if (IsDryRunBlockedSubmit(action, observation))
                {
                    var evidence = await CaptureStepEvidenceAsync(driver, observation, task.Task.Id, step, "dry-run");
                    var outcome = new ActionOutcome(false, "ready_for_submit", "Dry-run mode stopped before final submit.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, "dry_run_submit_blocked", new FailureRecord("dry_run_submit_blocked", "checkpoint", action.Reason, false), true);
                    var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, outcome.Message, false, "Review the application and submit manually if appropriate.", "manual_only");
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "dry_run_submit_blocked" }, outcome, recovery, evidence, stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ValidationAgent", LlmDecisionEngine.ClassifyPageState(observation), "dry_run_submit_blocked", action.Label, action.Reason, evidence, action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "dry_run_submit_blocked", "review", action.Label,
                        $"Dry-run mode stopped before final submit. job={task.Lead.Title}; company={task.Lead.Company}; url={observation.Url}; confidence={action.Confidence:0.00}; reason={action.Reason}",
                        runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "ready_for_submit", "dry_run_waiting_for_user_review");
                    await _client.CompleteRunAsync(runId, "ready_for_submit");
                    return false;
                }

                if (IsAutoSubmitDisabledBlockedSubmit(action, observation))
                {
                    var evidence = await CaptureStepEvidenceAsync(driver, observation, task.Task.Id, step, "auto-submit-disabled");
                    var outcome = new ActionOutcome(false, "ready_for_submit", "Auto-submit is disabled; stopped before final submit.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, "auto_submit_disabled", new FailureRecord("auto_submit_disabled", "checkpoint", action.Reason, false), true);
                    var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, outcome.Message, false, "Review the final application state and submit manually if appropriate.", "manual_only");
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "auto_submit_disabled" }, outcome, recovery, evidence, stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ValidationAgent", LlmDecisionEngine.ClassifyPageState(observation), "auto_submit_disabled", action.Label, action.Reason, evidence, action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "auto_submit_disabled", "review", action.Label,
                        $"Auto-submit is disabled. job={task.Lead.Title}; company={task.Lead.Company}; url={observation.Url}; confidence={action.Confidence:0.00}; reason={action.Reason}",
                        runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "ready_for_submit", "auto_submit_disabled_waiting_for_user_review");
                    await _client.CompleteRunAsync(runId, "ready_for_submit");
                    return false;
                }

                if (IsReviewEditAction(action, observation))
                {
                    var outcome = new ActionOutcome(false, "ready_for_submit", "Review screen reached; edit action would navigate backward.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, "pre_submit_review", new FailureRecord("pre_submit_review", "checkpoint", action.Reason, false), true);
                    var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, outcome.Message, false, "Review the final application state before submitting.", "manual_only");
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "review_edit_blocked" }, outcome, recovery, "", stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ValidationAgent", LlmDecisionEngine.ClassifyPageState(observation), "pre_submit_review", action.Label, action.Reason, "", action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "pre_submit_review", "review", "", $"PreSubmitReview domain={new Uri(observation.Url).Host}; job={task.Lead.Title}; company={task.Lead.Company}; url={observation.Url}; fields={observation.Elements.Count(e => e.IsField)}; confidence={action.Confidence:0.00}; reason=review_edit_blocked; proposed={action.Label}", runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "ready_for_submit", "waiting_for_user_review");
                    await _client.CompleteRunAsync(runId, "ready_for_submit");
                    return false;
                }

                if (action.Type == "submit" && !_decision.CanSubmitAutomatically(observation))
                {
                    var outcome = new ActionOutcome(false, "ready_for_submit", "Submit requires pre-submit review.", observationFrame.Fingerprint, observationFrame.Fingerprint, false, "pre_submit_review", new FailureRecord("pre_submit_review", "checkpoint", action.Reason, false), true);
                    var recovery = new RecoveryDecision(RecoveryMode.Checkpoint, outcome.Message, false, "Review the final application state before submitting.", "manual_only");
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "pre_submit_review" }, outcome, recovery, "", stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ValidationAgent", LlmDecisionEngine.ClassifyPageState(observation), "pre_submit_review", action.Label, action.Reason, "", action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, "pre_submit_review", "review", "", $"PreSubmitReview domain={new Uri(observation.Url).Host}; job={task.Lead.Title}; company={task.Lead.Company}; url={observation.Url}; fields={observation.Elements.Count(e => e.IsField)}; confidence={action.Confidence:0.00}; reason={action.Reason}", runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "ready_for_submit", "waiting_for_user_review");
                    await _client.CompleteRunAsync(runId, "ready_for_submit");
                    return false;
                }

                if (!actionVerdict.Allowed || actionVerdict.ApprovedAction is null)
                {
                    var outcome = new ActionOutcome(false, "blocked_by_guard", actionVerdict.Reason, observationFrame.Fingerprint, observationFrame.Fingerprint, false, actionVerdict.Verdict, new FailureRecord(actionVerdict.Verdict, "guard", actionVerdict.Reason, false), true);
                    var recovery = _recovery.Decide(outcome, actionVerdict);
                    var traceStepId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, new { skipped = true, reason = "blocked_by_guard" }, outcome, recovery, "", stepStartedAt);
                    await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "GuardAgent", LlmDecisionEngine.ClassifyPageState(observation), actionVerdict.Verdict, action.Label, actionVerdict.Reason, "", action.Confidence));
                    await CheckpointAsync(page, task.Task.Id, actionVerdict.Verdict, "runtime", action.Label, actionVerdict.Reason, runId, traceStepId, recovery.SuggestedUserAction, recovery.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "needs_review", actionVerdict.Verdict);
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                await _client.EventAsync(task.Task.Id, "runtime.action", $"{action.Type} {action.ElementId} {action.Label} confianca={action.Confidence:0.00}; {action.Reason}");
                if (action.Reason.StartsWith("Local fallback", StringComparison.OrdinalIgnoreCase))
                {
                    await _client.EventAsync(task.Task.Id, "llm.fallback_used",
                        $"action={action.Type}; element={action.ElementId}; confidence={action.Confidence:0.00}; reason={action.Reason}");
                }
                if (action.Type is "click" or "submit" && PageStateClassifier.LooksLikeNextElement(observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId) ?? new ElementCandidate("", "", "", "", action.Label, "", "", false, false, false, "", [], new ElementBounds(0, 0, 0, 0))))
                {
                    var repeated = session.RecordNavigationAction(action);
                    if (repeated >= 2)
                    {
                        var repeatProposal = ActionProposal.FromAction(BrowserAction.Checkpoint(action.ElementId, action.Label, "The same advance/review control stayed visible after repeated clicks."), "stuck_detector", requiresCheckpoint: true);
                        var traceStepId = await RecordCheckpointTraceAsync(runId, task.Task.Id, step, driver, observation, decisionFrame, contentSafety, repeatProposal, "validation_failed", "Review hidden validation errors before resuming.", stepStartedAt);
                        await CheckpointAsync(page, task.Task.Id, "validation_failed", "runtime", action.Label,
                            "The same advance/review control stayed visible after repeated clicks. The form likely has a hidden validation error or blocked submit state.",
                            runId, traceStepId, "Review hidden validation errors before resuming.", "reobserve");
                        await _client.SetStateAsync(task.Task.Id, "needs_review", "validation_failed");
                        await _client.CompleteRunAsync(runId, "needs_review");
                        return false;
                    }
                }
                else if (action.Type is "fill" or "select" or "check" or "upload_file" or "scroll")
                {
                    session.ResetNavigationAction();
                }

                var beforeEvidence = await CaptureStepEvidenceAsync(driver, observation, task.Task.Id, step, "before");
                var approvedAction = actionVerdict.ApprovedAction!;
                var executed = await ExecuteApprovedActionAsync(driver, page, observation, approvedAction, task, session);
                var afterObservation = await driver.ObserveAsync();
                var afterEvidence = await CaptureStepEvidenceAsync(driver, afterObservation, task.Task.Id, step, "after");
                var evidencePath = $"{beforeEvidence}; {afterEvidence}";
                session.RecordDecisionOutcome(action, executed.Success, executed.Message);
                var afterFrame = CreateObservationFrame(afterObservation, platform, stateMachine);
                var outcomeAfterExecution = _postActionValidator.ValidateOutcome(observationFrame, approvedAction, afterFrame, executed, session);
                var recoveryAfterExecution = _recovery.Decide(outcomeAfterExecution, actionVerdict);
                if (!outcomeAfterExecution.Success && recoveryAfterExecution.Mode == RecoveryMode.Checkpoint)
                {
                    var platformRecovery = platform.Recover(outcomeAfterExecution, decisionFrame);
                    if (platformRecovery.ContinueLoop && platformRecovery.Mode != RecoveryMode.None)
                        recoveryAfterExecution = platformRecovery;
                }
                var traceId = await RecordTraceStepAsync(runId, task.Task.Id, step, decisionFrame, contentSafety, proposal, actionRisk, actionVerdict, executed, outcomeAfterExecution, recoveryAfterExecution, evidencePath, stepStartedAt, afterFrame);

                // Atualiza state machine com a acao executada
                stateMachine.Transition(observation, action);

                await _client.StepAsync(new AgentStepRequest(runId, task.Task.Id, step, "ApplicationAgent", LlmDecisionEngine.ClassifyPageState(observation), action.Type, action.Label, executed.Message, evidencePath, action.Confidence));
                if (executed.Success)
                {
                    if (appliedLearning is not null)
                        await _client.LearningOutcomeAsync(new AgentLearningOutcomeRequest(appliedLearning.PatternKey, true, task.Task.Id, "Approved learning used successfully."));
                    await RememberWorkflowAsync(observation, action, task.Task.Id);
                }
                else
                {
                    if (appliedLearning is not null)
                        await _client.LearningOutcomeAsync(new AgentLearningOutcomeRequest(appliedLearning.PatternKey, false, task.Task.Id, executed.Message));

                    // Self-healing: se e retriavel, tenta continuar em vez de parar
                    if (recoveryAfterExecution.ContinueLoop && recoveryAfterExecution.Mode is RecoveryMode.Retry or RecoveryMode.Reobserve or RecoveryMode.Remap or RecoveryMode.Scroll)
                    {
                        await _client.EventAsync(task.Task.Id, "runtime.recovery",
                            $"Action failed; recovery={recoveryAfterExecution.Mode}; detail={recoveryAfterExecution.Reason}. Retry {session.RetryCount}/3.");
                        continue; // Tenta o proximo ciclo do loop
                    }

                    await CheckpointAsync(page, task.Task.Id, executed.CheckpointCode ?? "action_failed", "runtime", action.Label, executed.Message, runId, traceId, recoveryAfterExecution.SuggestedUserAction, recoveryAfterExecution.ResumeMode);
                    await _client.SetStateAsync(task.Task.Id, "needs_review", executed.CheckpointCode ?? "action_failed");
                    await _client.CompleteRunAsync(runId, "needs_review");
                    return false;
                }

                await Humanizer.DelayAsync(700, 1400);
                if (page is null)
                    continue;

                var selectedPage = await SelectBestPageAsync(page);
                if (selectedPage != page)
                {
                    page = selectedPage;
                    platform = ResolvePlatformForPage(page, platform);
                    driver = CreateBrowserDriver(page);
                }
            }

            await CheckpointAsync(page, task.Task.Id, "step_limit", "runtime", "", "Intelligent runtime reached its step limit.", runId, null, "Review current progress and resume with a fresh observation.", "reobserve");
            await _client.SetStateAsync(task.Task.Id, "needs_review", "step_limit");
            await _client.CompleteRunAsync(runId, "needs_review");
            return false;
        }
        catch
        {
            await _client.CompleteRunAsync(runId, "failed");
            throw;
        }
    }

}
