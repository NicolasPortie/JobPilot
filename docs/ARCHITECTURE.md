# JobPilot Architecture

JobPilot is organized as an operational system for job discovery, application execution, audit, and human review. The current MVP direction is extension-first, but the product target is hybrid execution: browser extension for consent/review/fallback, dedicated local browser for near-term background execution, and isolated remote/server-side workers for SaaS maturity.

For the detailed target design of the browser-agent harness and decision loop, see
[`AGENT_HARNESS_ARCHITECTURE.md`](AGENT_HARNESS_ARCHITECTURE.md).

For the execution strategy that avoids locking the user's main browser, see
[`SERVER_SIDE_AUTOMATION_STRATEGY.md`](SERVER_SIDE_AUTOMATION_STRATEGY.md).

## Runtime Boundaries

- `apps/api`: source of truth for candidate profile, resume path, filters, policies, saved answers, job leads, application queue, audit events, checkpoints, learnings, site memory, and LLM provider configuration.
- `apps/extension`: Chrome MV3 extension that observes supported job sites, marks actionable DOM elements, captures page context, and executes approved atomic commands in the user's real browser session.
- `apps/agent`: deterministic runtime/harness that normalizes observations, audits risk, plans actions, validates execution, records evidence, and asks for checkpoints when safe progress is not possible. The legacy browser driver remains useful for development and regression tests while discovery and platform navigation are progressively moved behind the extension transport.
- `apps/web`: operational panel for configuration, queue visibility, audit, checkpoints, and learning review.
- `infra`: Postgres and runtime infrastructure.
- `documents`: real local candidate documents referenced by profile/configuration. The agent must never invent document paths.

## Agent Harness Pipeline

The application agent follows this pipeline:

1. Observe: collect active page/modal state, visible text, elements, validation errors, scrollability, and screenshot evidence.
2. Normalize: convert platform-specific controls into semantic fields and actions. LinkedIn custom dropdowns and comboboxes with placeholders such as `Selecionar opcao` are normalized as required select/combobox fields.
3. Audit: detect required empty fields, placeholder selections, unanswered radio/checkbox groups, missing required uploads, visible validation errors, resume already selected, and sensitive questions.
4. Decide: prefer deterministic safe rules. Use the LLM only with normalized state, strict JSON schema, observed element IDs, and guard validation.
5. Execute: run validated actions through `IBrowserDriver`. The browser extension is the target production transport; any legacy/dev browser adapter must implement the same contract.
6. Validate: confirm post-action effects. Failed fills, unchanged selections, repeated validation errors, or unchanged navigation become correction attempts or checkpoints.
7. Learn: save learning only after validated progress and with strong context. Generic click/submit learnings are not auto-applied or auto-approved.
8. Checkpoint: persist reason, field, evidence, screenshot, and visible text whenever the harness cannot answer or proceed safely.

The runtime now records the richer loop envelope for each iteration:

- `ObservationFrame` and `DecisionFrame` capture the objective, platform/module, phase, page fingerprint, audit state, job context, and planner policy.
- Planners emit `ActionProposal`; they do not execute browser work.
- `ActionRiskClassifier` combines submit boundaries, sensitive fields, platform budget, and observed-content safety signals.
- `BrowserActionGuard` returns `ActionVerdict` and only approved proposals become `ApprovedAction`.
- The executor path accepts `ApprovedAction`; post-action validation returns `ActionOutcome`.
- `RecoveryEngine` and the active `IPlatformModule` turn outcomes into retry, reobserve, remap, checkpoint, or abort decisions.
- `TraceRedactor` redacts secrets and CPF-like values before trace payloads are sent to the API.

## Browser Boundary

The harness core depends on `IBrowserDriver` / `IObservationDriver`, not directly on Playwright:

- `ObserveAsync`
- `ClickAsync`
- `FillAsync`
- `SelectAsync`
- `CheckAsync`
- `UploadFileAsync`
- `ScrollAsync`
- `ScreenshotAsync`
- `CurrentUrl`

`BrowserExtensionDriver` is the target production adapter over `IBrowserExtensionTransport`: it observes, screenshots, and dispatches atomic browser commands, while business rules and intelligence stay in the harness. The extension content script must not decide what to fill, when to submit, or which risk policy applies.

`PlaywrightBrowserDriver` remains a legacy/dev adapter. The runtime can select the extension action/observation transport with `--browser-driver extension` or `JOBPILOT_BROWSER_DRIVER=extension`; LinkedIn discovery and initial lead opening already have extension-backed paths, while deeper platform-specific surface handling continues to move behind the extension boundary.

## Platform Boundary

Platform-specific behavior belongs under `apps/agent/Platforms`.

- `IPlatformModule` and `PlatformRegistry`: choose the platform module for a lead and expose flow, observation extension, readiness checks, and recovery.
- `GenericPlatformModule`: cautious fallback for unknown job sites.
- `LinkedInPlatformModule`: LinkedIn-specific readiness, login/security checkpointing, Easy Apply flow, observation extension, and recovery hints.
- `LinkedInNavigator`: compatibility facade used by discovery and runtime wiring.
- `LinkedInBrowserContextLauncher`: browser profile/channel setup for LinkedIn sessions.
- `LinkedInDiscoveryService`: search URL generation from DB-backed filters.
- `LinkedInLoginService`: login detection and credential-backed login attempt.
- `LinkedInEasyApplySurface`: Easy Apply and external `Candidatar-se` surface opening. Easy Apply remains inside the LinkedIn module; external apply captures popup/new tab or same-tab navigation, emits `linkedin.external_apply_opened`, and hands the active page to the generic runtime.
- `Platforms/LinkedIn/LinkedInPageNormalizer.cs`: LinkedIn DOM to semantic application fields.
- `Platforms/LinkedIn/LinkedInSelectors.cs`: LinkedIn selectors shared by navigation and application loading.
- `Platforms/LinkedIn/LinkedInObservationExtension.cs`: LinkedIn active-surface, modal, scroll, and field-context selectors for page observation.
- `Platforms/LinkedIn/LinkedInApplicationFlow.cs`: LinkedIn-specific navigation, post-login recovery, and Easy Apply surface opening.

The core harness should not accumulate LinkedIn-specific business decisions. It may call platform normalizers and then operate on semantic observations.

`ApplicationAgent` treats the selected module as the platform boundary. LinkedIn-specific login/security handling lives in the LinkedIn module and returns a platform checkpoint request when human action is needed. When LinkedIn opens an external application surface, the agent reobserves the external page, switches the active platform to `GenericPlatformModule`, and continues with the same planner, guard, submit policy, trace redaction, and checkpoint protocol. `manual_only` is reserved for real blockers such as login, captcha, blocked site, missing form, or policy-required human review, not for the mere absence of Easy Apply.

## Trace And Audit

The API persists rich trace in `AgentTraceStep`, linked to `AgentRun` and `ApplicationJob` when available. The trace stores objective JSON, platform/module, phase, before/after fingerprints, planner, proposal JSON, risk JSON, guard verdict JSON, execution JSON, outcome JSON, recovery JSON, content-safety JSON, evidence path, and timestamps.

Endpoints:

- `POST /api/agent/trace-steps`: agent-only write endpoint for rich runtime trace.
- `GET /api/application-jobs/{id}/trace-steps`: trace by application job.
- `GET /api/jobs/{id}/trace-steps`: trace by job lead.
- `GET /api/jobs/{id}/audit`: aggregate audit payload for the frontend, including legacy steps plus rich trace.

The web app consumes the aggregate audit payload and shows a technical timeline with proposal, risk, guard, execution, outcome, recovery/checkpoint, content-safety signals, and evidence path. If no rich trace exists, it falls back to legacy `AgentStep` records. The timeline includes operational summaries and filters for risk, planner, checkpoint/block state, safety signals, submit boundaries, and free-text search across trace JSON.

## Checkpoint Resume

Checkpoints now include optional `AgentRunId`, `TraceStepId`, `SuggestedUserAction`, and `ResumeMode`. The default resume mode is `reobserve`, so a resumed run is expected to observe the current browser state before continuing. Resolution statuses remain flexible enough for `resolved`, `stale`, `aborted`, `manual_done`, and existing statuses. Resolved answer-confirmation checkpoints can feed reusable screening answers.

## Safety Rules

- The LLM cannot execute browser actions directly.
- LLM action plans must match observed element IDs and compatible element types.
- Navigation actions are blocked while required fields, placeholder selects, unanswered required groups, required file inputs, or visible validation errors remain.
- Resume upload uses only the configured resume path from the profile/API.
- If the resume is already selected on screen, the agent must not click or upload resume again.
- In extension mode, local file selection is a human checkpoint. The extension must not pretend it can choose a private local file without the user's file picker action.
- Unknown or sensitive facts require checkpoint unless there is an explicit configured answer/source.
- Fallbacks must fail closed when validation or parsing is unreliable.
- Numeric experience answers must come from profile/reusable sources or checkpoint. Unknown technologies may be answered as `0` when the harness can identify the technology and confirm it is absent from the profile.

## Remaining Engineering Watchpoints

- `AgentStep` remains for backward-compatible summaries. New technical audit should prefer `AgentTraceStep`.
- Browser extension support now has an MV3 scaffold, local pairing, heartbeat, command queue, HTTP-backed driver transport, a runtime feature flag for extension observation/actions, initial LinkedIn discovery via extension commands, and extension-backed opening of the target lead before the application loop. The remaining work is moving the rest of platform-specific surface handling behind the extension boundary and replacing polling with a more immediate channel if MVP latency becomes a problem.
- Golden traces cover submit checkpoint and prompt-injection checkpoint. More golden traces should be added as real sites and ATS flows are exercised.
- Fallback answer paths should continue to be reviewed whenever new answer categories are added. Any fallback that cannot cite profile data, saved answers, or an approved policy must produce review/checkpoint instead of confident automation.

## Database Ownership

Business configuration should live in the database/API, not hardcoded in the agent:

- role keyword allow/block lists
- seniority and workplace filters
- apply type filters
- candidate profile and resume path
- saved answers
- auto-submit/review policies
- LLM provider configuration
- approved/rejected learnings
- site workflow memory
- checkpoints and audit trail

Code defaults are allowed only for bootstrap, migrations, or safe deterministic behavior.
