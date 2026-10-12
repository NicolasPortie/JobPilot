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
    private readonly AgentOptions _options;
    private readonly JobPilotApiClient _client;
    private readonly LocalAnswerResolver _resolver;
    private readonly ApplicationPlatformRouter _platforms;
    private readonly PlatformRegistry _platformRegistry;
    private readonly FieldValidator _validator = new();
    private readonly ErrorClassifier _errorClassifier = new();
    private readonly PostActionValidator _postActionValidator;
    private readonly PageStateClassifier _pageStateClassifier = new();
    private readonly FieldValuePolicy _fieldValuePolicy;
    private readonly FormAuditor _formAuditor;
    private readonly IObservedContentSafetyScanner _contentSafetyScanner = new ObservedContentSafetyScanner();
    private readonly ActionRiskClassifier _riskClassifier = new();
    private readonly RecoveryEngine _recovery = new();
    private readonly TraceRedactor _traceRedactor = new();
    private readonly ApplicationAgentRuntimeLoader _runtimeLoader;
    private LlmDecisionEngine _decision;
    private List<AgentLearning> _activeLearnings = [];
    private List<PlatformCredential> _platformCredentials = [];

    private bool _loginCheckpointCreated;

    public ApplicationAgent(AgentOptions options, JobPilotApiClient client, LocalAnswerResolver resolver, LinkedInNavigator navigator, LlmClient? llm = null)
    {
        _options = options;
        _client = client;
        _resolver = resolver;
        _platformRegistry = new PlatformRegistry([new LinkedInPlatformModule(navigator), new InhirePlatformModule()], GenericPlatformModule.Instance);
        _platforms = new ApplicationPlatformRouter(_platformRegistry);
        _runtimeLoader = new ApplicationAgentRuntimeLoader(client, resolver, llm);
        _fieldValuePolicy = new FieldValuePolicy(resolver);
        _formAuditor = new FormAuditor(_pageStateClassifier, _fieldValuePolicy);
        _postActionValidator = new PostActionValidator(_validator, _errorClassifier, resolver);
        _decision = new LlmDecisionEngine(llm ?? new LlmClient(client, false), resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
    }

    public async Task RunAsync(IBrowserContext context)
    {
        var runtime = await _runtimeLoader.LoadAsync();
        if (runtime is null)
            return;

        ApplyRuntime(runtime);
        var policy = runtime.Policy;

        var applied = 0;
        var processed = 0;
        Guid specificTaskId = Guid.Empty;
        var specificTaskMode = !string.IsNullOrWhiteSpace(_options.TaskId) && Guid.TryParse(_options.TaskId, out specificTaskId);
        AgentTaskResponse? task = null;

        // Se um TaskId especifico foi informado, busca apenas ele
        if (specificTaskMode)
        {
            Console.WriteLine($"[Run] Buscando task especifica: {specificTaskId}");
            task = await _client.GetTaskAsync(specificTaskId);
            if (task is null)
            {
                Console.WriteLine($"Task {specificTaskId} nao encontrada.");
                return;
            }
            _resolver.SetProfile(task.Profile, task.ResolveResumePath());
            var result = await ProcessSingleTaskSafelyAsync(context, task, policy);
            if (result) applied++;
            Console.WriteLine($"Parado apos processar task especifica. Aplicadas: {applied}");
            await _client.EventAsync(null, "run.completed", $"Task especifica processada. Aplicadas: {applied}");
            return;
        }

        while (processed < _options.MaxApplications)
        {
            task = await _client.GetNextTaskAsync();
            if (task is null)
            {
                Console.WriteLine("Nenhuma vaga na fila.");
                break;
            }
            processed++;

            _resolver.SetProfile(task.Profile, task.ResolveResumePath());

            if (policy?.IsPaused == true)
            {
                Console.WriteLine("Piloto automatico pausado. Pulando execucao.");
                await _client.EventAsync(task.Task.Id, "run.skipped", "Piloto automatico pausado.");
                continue;
            }

            if (policy?.BlockedDomains.Any(d => task.Lead.Url.Contains(d, StringComparison.OrdinalIgnoreCase)) == true)
            {
                await _client.SetStateAsync(task.Task.Id, "ignored", "blocked_domain");
                continue;
            }

            if (policy is not null && policy.AllowedDomains.Length > 0 && !policy.AllowedDomains.Any(d => task.Lead.Url.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                await _client.SetStateAsync(task.Task.Id, "ignored", "domain_not_allowed");
                continue;
            }

            var success = await ProcessSingleTaskSafelyAsync(context, task, policy);
            if (success) applied++;
            await Humanizer.DelayAsync(2000, 4000);
        }

        Console.WriteLine($"Parado apos processar {processed} vaga(s). Aplicadas: {applied}.");
        await _client.EventAsync(null, "run.limit_reached", $"Parado apos processar {processed} vaga(s). Aplicadas: {applied}.");
    }

    public async Task RunExtensionOnlyAsync()
    {
        var runtime = await _runtimeLoader.LoadAsync();
        if (runtime is null)
            return;

        ApplyRuntime(runtime);
        var policy = runtime.Policy;

        var applied = 0;
        var processed = 0;
        while (processed < _options.MaxApplications)
        {
            var task = await _client.GetNextTaskAsync();
            if (task is null)
            {
                Console.WriteLine("Nenhuma vaga na fila.");
                break;
            }

            processed++;
            _resolver.SetProfile(task.Profile, task.ResolveResumePath());

            if (policy?.IsPaused == true)
            {
                await _client.EventAsync(task.Task.Id, "run.skipped", "Piloto automatico pausado.");
                continue;
            }

            var platform = _platforms.ResolveModule(task.Lead);
            var success = await ProcessSingleTaskWithExtensionAsync(null, task, platform);
            if (success) applied++;
            await Humanizer.DelayAsync(2000, 4000);
        }

        Console.WriteLine($"Parado apos processar {processed} vaga(s). Aplicadas: {applied}.");
        await _client.EventAsync(null, "run.limit_reached", $"Parado apos processar {processed} vaga(s). Aplicadas: {applied}.");
    }

    private async Task<bool> ProcessSingleTaskSafelyAsync(IBrowserContext context, AgentTaskResponse task, AgentPolicy? policy)
    {
        try
        {
            return await ProcessSingleTaskAsync(context, task, policy);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("closed", StringComparison.OrdinalIgnoreCase))
        {
            await _client.EventAsync(task.Task.Id, "browser.closed", ex.Message);
            await _client.SetStateAsync(task.Task.Id, "needs_review", "browser_closed");
            return false;
        }
    }

    private void ApplyRuntime(ApplicationAgentRuntime runtime)
    {
        _decision = runtime.Decision;
        _activeLearnings = runtime.ActiveLearnings;
        _platformCredentials = runtime.PlatformCredentials;
    }

    private async Task<bool> ProcessSingleTaskAsync(IBrowserContext context, AgentTaskResponse task, AgentPolicy? policy)
    {
        var page = context.Pages.FirstOrDefault(p => !p.IsClosed) ?? await context.NewPageAsync();
        var platform = _platforms.ResolveModule(task.Lead);
        if (string.Equals(_options.BrowserDriverMode, "extension", StringComparison.OrdinalIgnoreCase))
            return await ProcessSingleTaskWithExtensionAsync(page, task, platform);

        await platform.Flow.NavigateToLeadAsync(page, task);
        if (await EnsurePlatformReadyAsync(platform, page, task.Task.Id, "pre_apply"))
        {
            await _client.SetStateAsync(task.Task.Id, "needs_review", "login_or_security");
            return false;
        }
        await platform.Flow.RecoverAfterLoginAsync(page, task);
        var applicationSurface = await platform.Flow.OpenApplicationSurfaceAsync(page, task);
        if (!applicationSurface.Opened)
        {
            await PersistApplySurfaceAsync(task, applicationSurface, page.Url);
            if (applicationSurface.FailureCode == "already_applied")
            {
                await _client.EventAsync(task.Task.Id, "job.already_applied", applicationSurface.Detail);
                await _client.SetStateAsync(task.Task.Id, "applied", "already_applied");
                return false;
            }

            await _client.EventAsync(task.Task.Id, "application_surface.missing", applicationSurface.Detail);
            await CheckpointAsync(page, task.Task.Id, applicationSurface.FailureCode, "pre_apply", applicationSurface.FieldLabel, applicationSurface.Detail);
            await _client.SetStateAsync(task.Task.Id, applicationSurface.FailureState, applicationSurface.FailureCode);
            return false;
        }

        if (applicationSurface.ActivePage is not null && applicationSurface.ActivePage != page)
            page = applicationSurface.ActivePage;

        await PersistApplySurfaceAsync(task, applicationSurface, page.Url);

        if (applicationSurface.SurfaceKind == "inhire")
        {
            platform = ResolvePlatformForPage(page, platform);
            await _client.EventAsync(task.Task.Id, "linkedin.external_apply_opened",
                $"surface={applicationSurface.SurfaceKind}; url={page.Url}; detail={applicationSurface.Detail}");
        }
        else if (applicationSurface.SurfaceKind == "unsupported_external")
        {
            await _client.EventAsync(task.Task.Id, "application_surface.unsupported",
                $"surface={applicationSurface.SurfaceKind}; url={page.Url}; detail={applicationSurface.Detail}");
            await _client.SetStateAsync(task.Task.Id, "manual_only", "unsupported_external");
            return false;
        }
        else if (!string.IsNullOrWhiteSpace(applicationSurface.SurfaceKind))
        {
            await _client.EventAsync(task.Task.Id, "application_surface.opened",
                $"surface={applicationSurface.SurfaceKind}; url={page.Url}; detail={applicationSurface.Detail}");
        }

        await Humanizer.DelayAsync(1500, 2500);
        var success = await RunApplicationLoopAsync(page, task, platform, applicationSurface);
        return success;
    }

    private async Task<bool> ProcessSingleTaskWithExtensionAsync(IPage? page, AgentTaskResponse task, IPlatformModule platform)
    {
        var transport = new BrowserExtensionHttpTransport(_client);
        var targetUrl = BuildExtensionLeadUrl(task.Lead);
        await transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: targetUrl, ReasonCode: "pre_apply.navigate"));
        await _client.EventAsync(task.Task.Id, "extension.lead_opened", $"url={targetUrl}");

        var observation = await transport.ObserveAsync();
        if (LooksLikeLoginOrSecurityPage(observation))
        {
            var surface = new ApplicationSurfaceOpenResult(false, "login_or_security", "needs_review", "Login", "Login or security checkpoint is visible in the user's browser.");
            await PersistApplySurfaceAsync(task, surface, observation.Url);
            if (page is null)
                await _client.CheckpointAsync(task.Task.Id, "login_or_security", "pre_apply", "Login", "Abra o LinkedIn no Chrome, finalize login/2FA/captcha e rode novamente.");
            else
                await CheckpointAsync(page, task.Task.Id, "login_or_security", "pre_apply", "Login", "Abra o LinkedIn no Chrome, finalize login/2FA/captcha e rode novamente.");
            await _client.SetStateAsync(task.Task.Id, "needs_review", "login_or_security");
            return false;
        }

        if (platform.Name.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase))
        {
            var jobIdentityFailure = await EnsureLinkedInExpectedJobInExtensionAsync(transport, task, targetUrl, observation);
            if (jobIdentityFailure.Failure is not null)
            {
                var mismatchSurface = jobIdentityFailure.Failure;
                await PersistApplySurfaceAsync(task, mismatchSurface, mismatchSurface.FinalUrl);
                await _client.EventAsync(task.Task.Id, "application_surface.missing", mismatchSurface.Detail);
                await _client.CheckpointAsync(
                    task.Task.Id,
                    mismatchSurface.FailureCode,
                    "pre_apply",
                    mismatchSurface.FieldLabel,
                    _traceRedactor.RedactText($"{mismatchSurface.Detail} Evidence: extensionUrl={mismatchSurface.FinalUrl}; observedUrl={jobIdentityFailure.Observation.Url}; observedText={jobIdentityFailure.Observation.VisibleText[..Math.Min(jobIdentityFailure.Observation.VisibleText.Length, 1200)]}", TraceRedactionScope.TracePersistence));
                await _client.SetStateAsync(task.Task.Id, mismatchSurface.FailureState, mismatchSurface.FailureCode);
                return false;
            }

            observation = jobIdentityFailure.Observation;
            var surface = await OpenLinkedInSurfaceWithExtensionAsync(transport, task, observation);
            await PersistApplySurfaceAsync(task, surface, surface.FinalUrl);

            if (!surface.Opened)
            {
                if (surface.FailureCode == "already_applied")
                {
                    await _client.EventAsync(task.Task.Id, "job.already_applied", surface.Detail);
                    await _client.SetStateAsync(task.Task.Id, "applied", "already_applied");
                    return false;
                }

                await _client.EventAsync(task.Task.Id, "application_surface.missing", surface.Detail);
                await _client.CheckpointAsync(
                    task.Task.Id,
                    surface.FailureCode,
                    "pre_apply",
                    surface.FieldLabel,
                    _traceRedactor.RedactText($"{surface.Detail} Evidence: extensionUrl={surface.FinalUrl}; observedUrl={observation.Url}; observedText={observation.VisibleText[..Math.Min(observation.VisibleText.Length, 1200)]}", TraceRedactionScope.TracePersistence));
                await _client.SetStateAsync(task.Task.Id, surface.FailureState, surface.FailureCode);
                return false;
            }

            if (surface.SurfaceKind == "unsupported_external")
            {
                await _client.EventAsync(task.Task.Id, "application_surface.unsupported",
                    $"surface={surface.SurfaceKind}; url={surface.FinalUrl}; detail={surface.Detail}");
                await _client.SetStateAsync(task.Task.Id, "manual_only", "unsupported_external");
                return false;
            }

            if (surface.SurfaceKind == "inhire")
                platform = _platformRegistry.Resolve(new JobLead { Url = surface.FinalUrl, ApplyUrl = surface.FinalUrl, ApplyPlatform = "inhire" });

            await _client.EventAsync(task.Task.Id, "application_surface.opened",
                $"surface={surface.SurfaceKind}; url={surface.FinalUrl}; detail={surface.Detail}");

            return await RunApplicationLoopAsync(page, task, platform, surface);
        }

        var applicationSurface = ApplicationSurfaceOpenResult.OpenedSurface(
            "extension_active_page",
            platform.Name,
            observation.Url,
            "Lead opened in the user's browser extension session.",
            page);
        await PersistApplySurfaceAsync(task, applicationSurface, observation.Url);
        await _client.EventAsync(task.Task.Id, "application_surface.opened",
            $"surface={applicationSurface.SurfaceKind}; url={observation.Url}; detail={applicationSurface.Detail}");

        return await RunApplicationLoopAsync(page, task, platform, applicationSurface);
    }

    private IBrowserDriver CreateBrowserDriver(IPage page) =>
        string.Equals(_options.BrowserDriverMode, "extension", StringComparison.OrdinalIgnoreCase)
            ? new BrowserExtensionDriver(new BrowserExtensionHttpTransport(_client))
            : new PlaywrightBrowserDriver(page);

    private static string BuildExtensionLeadUrl(JobLead lead)
    {
        if (lead.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
        {
            var jobId = LinkedInApplicationFlow.ExtractJobId(lead.Url);
            if (!string.IsNullOrWhiteSpace(jobId))
                return $"https://www.linkedin.com/jobs/view/{jobId}/";
        }

        return string.IsNullOrWhiteSpace(lead.ApplyUrl) ? lead.Url : lead.ApplyUrl;
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement element, string property, bool defaultValue) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : defaultValue;

    private static bool TitleLooksVisible(PageObservation observation, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        var text = AgentText.RemoveDiacritics(observation.VisibleText).ToLowerInvariant();
        var normalizedTitle = AgentText.RemoveDiacritics(title).ToLowerInvariant();
        if (text.Contains(normalizedTitle))
            return true;

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "with", "verification", "remote", "remoto", "jr", "mid", "pleno"
        };
        var tokens = Regex.Matches(normalizedTitle, @"[a-z0-9#.+]{2,}")
            .Select(m => m.Value)
            .Where(t => !ignored.Contains(t))
            .Distinct()
            .Take(6)
            .ToList();

        return tokens.Count == 0 || tokens.Count(t => text.Contains(t)) >= Math.Min(3, tokens.Count);
    }

    private static bool LinkedInPageShowsAlreadyApplied(PageObservation observation) =>
        AgentText.ContainsAny(observation.VisibleText, [
            "candidatura enviada",
            "candidatura enviada ha",
            "candidatura enviada ha",
            "voce se candidatou",
            "voce se candidatou",
            "status da candidatura candidatura enviada",
            "application submitted",
            "you applied"
        ]);

    private static bool LooksLikeEasyApplyModal(PageObservation observation) =>
        observation.HasModal ||
        AgentText.ContainsAny(observation.VisibleText, ["enviar candidatura", "submit application", "revise sua candidatura", "review your application"]) ||
        observation.Elements.Any(e => AgentText.ContainsAny(e.SearchText, [
            "avancar",
            "next",
            "revisar",
            "review",
            "enviar candidatura",
            "submit application"
        ]));

    private static bool LooksLikeLinkedInExternalApply(ElementCandidate element)
    {
        var searchText = element.SearchText;
        if (AgentText.ContainsAny(searchText, ["easy apply", "candidatura simplificada"]))
            return false;

        return AgentText.ContainsAny(searchText, [
            "candidatar-se",
            "apply",
            "apply on company website",
            "candidate-se no site da empresa"
        ]);
    }

    private static bool LooksLikeLoginOrSecurityPage(PageObservation observation)
    {
        var text = AgentText.RemoveDiacritics($"{observation.Title} {observation.VisibleText}").ToLowerInvariant();
        var url = observation.Url.ToLowerInvariant();
        var hasCredentialFields =
            (text.Contains("senha") || text.Contains("password")) &&
            (text.Contains("email") || text.Contains("e-mail") || text.Contains("telefone") || text.Contains("phone"));
        var hasSecurityChallenge =
            text.Contains("captcha") ||
            text.Contains("security check") ||
            text.Contains("identity verification") ||
            text.Contains("verificacao de seguranca") ||
            text.Contains("verifique sua identidade") ||
            url.Contains("/checkpoint/", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/login", StringComparison.OrdinalIgnoreCase);

        return hasSecurityChallenge ||
               hasCredentialFields && (text.Contains("sign in") || text.Contains("entrar") || text.Contains("login"));
    }

    private static string ManualResumeUploadMessage(string? resumePath)
    {
        var fileName = string.IsNullOrWhiteSpace(resumePath) ? "o curriculo configurado no perfil" : Path.GetFileName(resumePath);
        return $"A extensao nao seleciona arquivos locais automaticamente. Anexe manualmente {fileName} no navegador e retome a execucao para o JobPilot continuar.";
    }

    private async Task<bool> EnsurePlatformReadyAsync(IPlatformModule platform, IPage page, Guid? taskId, string stage)
    {
        var checkpoint = await platform.EnsureReadyAsync(page, taskId, stage, CredentialFor(platform));
        if (checkpoint is null) return false;

        if (_loginCheckpointCreated && checkpoint.ReasonCode == "login_required")
            return true;

        await CheckpointAsync(
            page,
            taskId,
            checkpoint.ReasonCode,
            checkpoint.Stage,
            checkpoint.FieldLabel,
            checkpoint.Detail,
            null,
            null,
            checkpoint.SuggestedUserAction,
            checkpoint.ResumeMode);

        if (checkpoint.ReasonCode == "login_required")
            _loginCheckpointCreated = true;

        return true;
    }

    private IPlatformModule ResolvePlatformForPage(IPage page, IPlatformModule current)
    {
        if (page.Url.Contains("inhire.app", StringComparison.OrdinalIgnoreCase))
            return _platformRegistry.Resolve(new JobLead { Url = page.Url, ApplyUrl = page.Url, ApplyPlatform = "inhire" });

        if (page.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
            return current;

        return GenericPlatformModule.Instance;
    }

    private async Task PersistApplySurfaceAsync(AgentTaskResponse task, ApplicationSurfaceOpenResult surface, string currentUrl)
    {
        var finalUrl = string.IsNullOrWhiteSpace(surface.FinalUrl) ? currentUrl : surface.FinalUrl;
        var request = surface.SurfaceKind switch
        {
            "linkedin_easy_apply" => new ApplySurfaceRequest("linkedin_easy_apply", "easy_apply_modal", finalUrl, "supported", "linkedin_easy_apply"),
            "inhire" => new ApplySurfaceRequest("inhire", "external_ats", finalUrl, "supported", "linkedin_external_inhire"),
            "unsupported_external" => new ApplySurfaceRequest("unsupported_external", "unsupported_external", finalUrl, "manual_only", "unsupported_external"),
            "already_applied" => new ApplySurfaceRequest("linkedin_easy_apply", "already_applied", finalUrl, "manual_only", "already_applied"),
            _ when surface.FailureCode == "already_applied" => new ApplySurfaceRequest("linkedin_easy_apply", "already_applied", finalUrl, "manual_only", "already_applied"),
            _ => new ApplySurfaceRequest("unknown", "unknown", finalUrl, "unknown", surface.FailureCode)
        };

        await _client.SetApplySurfaceAsync(task.Task.Id, request);
    }

    private PlatformCredential? CredentialFor(IPlatformModule platform) =>
        _platformCredentials.FirstOrDefault(c =>
            c.Platform.Equals(platform.Name, StringComparison.OrdinalIgnoreCase) ||
            c.Platform.Equals(platform.Name.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));

    private bool IsDryRunBlockedSubmit(BrowserAction action, PageObservation observation)
    {
        if (!_options.DryRun) return false;
        if (action.Type == "submit") return true;
        if (action.Type != "click") return false;

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        return element is not null && PageStateClassifier.LooksLikeSubmitElement(element);
    }

    private bool IsAutoSubmitDisabledBlockedSubmit(BrowserAction action, PageObservation observation)
    {
        if (_options.AutoSubmit) return false;
        if (action.Type == "submit") return true;
        if (action.Type != "click") return false;

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        return element is not null && PageStateClassifier.LooksLikeSubmitElement(element);
    }

    private static bool IsOutOfApplySurfaceAction(BrowserAction action, PageObservation observation, ApplicationSurfaceOpenResult surface)
    {
        if (surface.SurfaceKind != "linkedin_easy_apply") return false;
        if (!observation.HasModal) return false;
        if (action.Type is not ("click" or "submit")) return false;

        var actionText = AgentText.RemoveDiacritics($"{action.Label} {action.Reason}").ToLowerInvariant();
        return AgentText.ContainsAny(actionText, [
            "mensagens", "messaging", "message module", "lista de conversas",
            "notificacoes", "notifications",
            "minha rede", "my network",
            "feed", "pagina inicial", "home page",
            "premium", "solucoes", "for business",
            "perfil", "profile menu",
            "fechar", "close", "descartar", "discard", "cancelar candidatura", "cancel application"
        ]);
    }

    private static bool IsReviewEditAction(BrowserAction action, PageObservation observation)
    {
        if (action.Type != "click") return false;
        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        return element is not null &&
               PageStateClassifier.LooksLikeReviewEditElement(element) &&
               AgentText.ContainsAny(observation.VisibleText, ["revise sua candidatura", "review your application"]);
    }

    private static BrowserAction? SkipCompletedFieldAction(PageObservation observation, BrowserAction action, SessionContext session)
    {
        var isResumeCheckpoint = action.Type == "checkpoint" &&
            AgentText.ContainsAny($"{action.Label} {action.Reason} {observation.VisibleText}", ["curriculo", "resume", "cv", "upload", "carregue"]);

        var isResumeUploadClick = action.Type == "click" &&
            AgentText.ContainsAny($"{action.Label} {action.Reason}", ["carregue o curriculo", "carregar curriculo", "upload cv", "upload resume", "resume", "curriculo"]);

        if ((action.Type == "upload_file" || isResumeCheckpoint || isResumeUploadClick) && ResumeAlreadySelected(observation))
        {
            var nextUpload = observation.Elements.FirstOrDefault(e =>
                e.IsButtonLike &&
                PageStateClassifier.LooksLikeNextElement(e) &&
                !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText));
            if (nextUpload is not null)
            {
                return new BrowserAction(
                    "click",
                    nextUpload.ElementId,
                    "",
                    nextUpload.Label,
                    "Resume is already selected; advancing instead of uploading or checkpointing again.",
                    Math.Max(action.Confidence, 0.82m));
            }
        }

        if (action.Type is not ("fill" or "select" or "check")) return null;

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (element is null) return null;

        var fieldKey = StableFieldKey(element.SearchText);
        var hasValue = action.Type switch
        {
            "fill" => !string.IsNullOrWhiteSpace(element.Value) ||
                      !string.IsNullOrWhiteSpace(session.GetAssignedValue(fieldKey)),
            "select" => !FieldValuePolicy.IsEmptySelectValue(element) ||
                        !string.IsNullOrWhiteSpace(session.GetAssignedValue(fieldKey)),
            "check" => element.Checked || session.HasField(fieldKey),
            _ => false
        };
        if (!hasValue) return null;

        var next = observation.Elements.FirstOrDefault(e =>
            e.IsButtonLike &&
            PageStateClassifier.LooksLikeNextElement(e) &&
            !PageStateClassifier.LooksLikeUnsafeJobPageAction(e.SearchText));
        if (next is null) return null;

        return new BrowserAction(
            "click",
            next.ElementId,
            "",
            next.Label,
            $"Field '{element.Label}' already has a value; advancing instead of repeating {action.Type}.",
            Math.Max(action.Confidence, 0.78m));
    }

    private static bool ResumeAlreadySelected(PageObservation observation) =>
        observation.Elements.Any(e => e.IsFile && !string.IsNullOrWhiteSpace(e.Value)) ||
        AgentText.ContainsAny(observation.VisibleText, [
            ".pdf", ".doc", ".docx", "curriculo nicolas", "curriculo", "resume",
            "arquivo foi carregado", "carregado com sucesso", "upload concluido", "upload concluido",
            "file uploaded", "uploaded successfully",
            "ultima utilizacao", "ultima utilizacao", "desmarcar selecao", "desmarcar selecao"
        ]);

    private static bool LooksLikeUploadTrigger(BrowserAction action, ElementCandidate element) =>
        element.IsButtonLike &&
        AgentText.ContainsAny($"{action.Label} {action.Reason} {element.SearchTextWithHints}", [
            "anexar curriculo", "anexar curriculo", "enviar curriculo", "enviar curriculo",
            "upload cv", "upload resume", "attach resume", "attach cv",
            "selecionar arquivo", "escolher arquivo", "choose file", "select file",
            "carregue o curriculo", "carregar curriculo", "carregue seu curriculo"
        ]);

    private static async Task<string> CaptureStepEvidenceAsync(IBrowserDriver driver, PageObservation observation, Guid taskId, int step, string stage)
    {
        var safeStage = string.Concat(stage.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_'));
        var reasonCode = $"step-{step:00}-{safeStage}-{taskId.ToString()[..8]}";

        try
        {
            var evidence = await driver.ScreenshotAsync(reasonCode);
            if (string.IsNullOrWhiteSpace(evidence))
                return $"url={observation.Url}; screenshot=unavailable; visibleText={CompactVisibleText(observation.VisibleText)}";

            if (evidence.StartsWith("url=", StringComparison.OrdinalIgnoreCase))
                return evidence;

            if (TrySaveDataUrlEvidence(evidence, reasonCode, out var screenshotPath))
                return $"url={observation.Url}; screenshot={screenshotPath}; visibleText={CompactVisibleText(observation.VisibleText)}";

            return $"url={observation.Url}; screenshot={evidence}; visibleText={CompactVisibleText(observation.VisibleText)}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"url={observation.Url}; screenshot=unavailable; error={ex.Message}; visibleText={CompactVisibleText(observation.VisibleText)}";
        }
    }

    private static bool TrySaveDataUrlEvidence(string dataUrl, string reasonCode, out string screenshotPath)
    {
        screenshotPath = "";
        var match = Regex.Match(dataUrl, @"^data:image/(?<type>png|jpeg|webp);base64,(?<data>.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var extension = match.Groups["type"].Value.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : match.Groups["type"].Value.ToLowerInvariant();
        var evidenceDir = Path.Combine(AppContext.BaseDirectory, "evidence");
        Directory.CreateDirectory(evidenceDir);
        screenshotPath = Path.Combine(evidenceDir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{reasonCode}.{extension}");
        File.WriteAllBytes(screenshotPath, Convert.FromBase64String(match.Groups["data"].Value));
        return true;
    }

    private static string CompactVisibleText(string text)
    {
        var compact = Regex.Replace(text ?? "", "\\s+", " ").Trim();
        return compact[..Math.Min(compact.Length, 700)];
    }

    private async Task CheckpointAsync(
        IPage? page,
        Guid? taskId,
        string reasonCode,
        string stage,
        string fieldLabel,
        string detail,
        Guid? runId = null,
        Guid? traceStepId = null,
        string suggestedUserAction = "",
        string resumeMode = "reobserve")
    {
        var evidence = page is null ? "" : await PageObserver.CaptureEvidenceAsync(page, reasonCode);
        var detailWithEvidence = string.IsNullOrWhiteSpace(evidence) ? detail : $"{detail} Evidence: {evidence}";
        var redactedDetail = _traceRedactor.RedactText(detailWithEvidence, TraceRedactionScope.TracePersistence);
        await _client.CheckpointAsync(taskId, reasonCode, stage, fieldLabel, redactedDetail, runId, traceStepId, suggestedUserAction, resumeMode);
        if (taskId is not null)
            await _client.GenerateLearningsFromJobAsync(taskId.Value);
    }

    private async Task RegisterAnswerReviewAsync(Guid taskId, ElementCandidate element, string draftedAnswer, decimal confidence)
    {
        await _client.CheckpointAsync(
            taskId,
            "answer_needs_confirmation",
            "answer_review",
            element.Label,
            $"Resposta provisoria usada pelo agente precisa ser confirmada para virar resposta reutilizavel. confidence={confidence:0.00}; value={draftedAnswer}");
    }

    private static string StableFieldKey(string text)
    {
        var normalized = AgentText.RemoveDiacritics(text).ToLowerInvariant();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var compact = Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(compact) ? "field" : compact[..Math.Min(compact.Length, 80)];
    }

    private static string ResolveValueSource(string label, AgentTaskResponse task, string actionReason)
    {
        var normalized = label.ToLowerInvariant();
        if (task.Answers.Any(x => normalized.Contains(x.QuestionKey.ToLowerInvariant()) || normalized.Contains(x.QuestionText.ToLowerInvariant()))) return "reusable-answer";
        if (FormFiller.ResolveValue(label, task.Profile, task.Answers).Length > 0) return "candidate-profile";
        if (actionReason.Contains("numeric-experience-from-profile", StringComparison.OrdinalIgnoreCase)) return "candidate-profile";
        if (actionReason.Contains("ai-draft", StringComparison.OrdinalIgnoreCase)) return "ai-draft";
        return "ai-draft";
    }

    private static bool RequiresExplicitSource(ElementCandidate element, string valueSource)
    {
        if (!LlmDecisionEngine.IsSensitiveQuestion(element.SearchTextWithHints)) return false;
        return valueSource is not ("candidate-profile" or "reusable-answer" or "question-pattern");
    }

    private static string RedactIfSensitive(ElementCandidate element, string value) =>
        LlmDecisionEngine.IsSensitiveQuestion(element.SearchTextWithHints) ? "[redacted-sensitive-answer]" : value;

    private AgentLearning? TryApplyLearningSuggestion(PageObservation observation, BrowserAction action)
    {
        if (_activeLearnings.Count == 0) return null;
        if (action.Type is "checkpoint" or "done" or "wait" or "scroll") return null;
        if (action.Type is "click" or "submit") return null;
        if (!Uri.TryCreate(observation.Url, UriKind.Absolute, out var uri)) return null;
        if (action.Type == "click" && AgentText.ContainsAny($"{action.Label} {action.Reason}", ["curriculo", "resume", "cv", "upload", "carregue"]))
            return null;

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..].ToLowerInvariant()
            : uri.Host.ToLowerInvariant();
        var actionText = $"{action.Type} {action.Label} {action.Reason} {action.Value}".ToLowerInvariant();

        return _activeLearnings.FirstOrDefault(learning =>
            learning.Domain == host &&
            learning.RiskLevel == "low" &&
            learning.LearningType is "technical" or "format" &&
            !LlmDecisionEngine.IsSensitiveQuestion($"{learning.Title} {learning.Summary} {action.Label}") &&
            LearningMatchesAction(learning, actionText));
    }

    private static bool LearningMatchesAction(AgentLearning learning, string actionText)
    {
        var key = learning.PatternKey.ToLowerInvariant();
        var actionType = actionText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (key.Contains("-click-") && actionType != "click") return false;
        if (key.Contains("-fill-") && actionType != "fill") return false;
        if (key.Contains("-select-") && actionType != "select") return false;
        if (key.Contains("-submit-") && actionType != "submit") return false;
        if (key.StartsWith("click:", StringComparison.Ordinal) && actionType != "click") return false;
        if (key.StartsWith("fill:", StringComparison.Ordinal) && actionType != "fill") return false;
        if (key.StartsWith("select:", StringComparison.Ordinal) && actionType != "select") return false;
        if (key.StartsWith("submit:", StringComparison.Ordinal) && actionType != "submit") return false;

        if (key.Contains("confirmation") && actionText.Contains("done")) return true;
        if (key.Contains("combobox") && actionText.Contains("select")) return true;
        if (key.Contains("dropdown") && actionText.Contains("select")) return true;
        if (key.Contains("salary") && actionText.Contains("salary")) return true;
        if (key.Contains("avoid-back") && actionText.Contains("voltar")) return true;
        return key.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Where(IsSpecificLearningToken)
            .Any(part => actionText.Contains(part));
    }

    private static bool IsSpecificLearningToken(string part) =>
        part.Length >= 5 &&
        part is not ("click" or "fill" or "select" or "submit" or "technical" or "linkedin" or "com" or "para" or "proxima" or "etapa" or "botao");

    private async Task RememberWorkflowAsync(PageObservation observation, BrowserAction action, Guid taskId)
    {
        if (action.Type is not ("click" or "submit" or "select" or "check" or "upload_file")) return;
        if (action.Type is "click" or "submit") return;
        if (action.Type == "click" && AgentText.ContainsAny(action.Label, ["curriculo", "resume", "cv", "upload", "carregue"])) return;
        if (!Uri.TryCreate(observation.Url, UriKind.Absolute, out var uri)) return;
        var patternKey = $"{action.Type}:{StableFieldKey(action.Label)}";
        await _client.WorkflowMemoryAsync(new SiteWorkflowMemoryRequest(
            uri.Host.ToLowerInvariant(),
            patternKey,
            JsonSerializer.Serialize(new { action.Type, action.Label, action.Value, action.Confidence }),
            true));

        if (action.Type is "click" or "submit")
        {
            var learning = await _client.LearningCandidateAsync(new AgentLearningRequest(
                taskId,
                uri.Host.ToLowerInvariant(),
                "technical",
                patternKey,
                $"Acao de fluxo bem-sucedida: {action.Type}",
                $"O controle '{action.Label}' funcionou durante o fluxo de candidatura.",
                JsonSerializer.Serialize(new { observation.Url, action.Label }),
                JsonSerializer.Serialize(new { action.Type, action.ElementId, action.Label }),
                JsonSerializer.Serialize(new { observation.Title, action.Confidence }),
                "low"));
            if (learning is not null)
                await _client.LearningOutcomeAsync(new AgentLearningOutcomeRequest(learning.PatternKey, true, taskId, "Workflow action executed successfully."));
        }
    }

    private async Task RememberConfirmationLearningAsync(PageObservation observation, Guid taskId)
    {
        if (!Uri.TryCreate(observation.Url, UriKind.Absolute, out var uri)) return;
        var learning = await _client.LearningCandidateAsync(new AgentLearningRequest(
            taskId,
            uri.Host.ToLowerInvariant(),
            "technical",
            "confirmation_detection",
            "Reconhecer confirmacao de candidatura",
            "A tela atual indicou que a candidatura foi enviada com sucesso.",
            JsonSerializer.Serialize(new { observation.Url, text = observation.VisibleText[..Math.Min(observation.VisibleText.Length, 240)] }),
            JsonSerializer.Serialize(new { result = "mark_applied" }),
            JsonSerializer.Serialize(new { observation.Title, observation.HasModal }),
            "low"));
        if (learning is not null)
            await _client.LearningOutcomeAsync(new AgentLearningOutcomeRequest(learning.PatternKey, true, taskId, "Confirmation detected."));
    }
}

