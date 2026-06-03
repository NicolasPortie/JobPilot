using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Harness.Audit;
using JobPilot.Agent.Infrastructure;
using JobPilot.Agent.Platforms;
using JobPilot.Agent.Platforms.Inhire;
using JobPilot.Agent.Platforms.LinkedIn;
using Microsoft.Playwright;

namespace JobPilot.Agent.Application;

public sealed class ApplicationAgent
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
    private readonly LlmClient? _llm;
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
        _llm = llm;
        _fieldValuePolicy = new FieldValuePolicy(resolver);
        _formAuditor = new FormAuditor(_pageStateClassifier, _fieldValuePolicy);
        _postActionValidator = new PostActionValidator(_validator, _errorClassifier, resolver);
        _decision = new LlmDecisionEngine(llm ?? new LlmClient(client, false), resolver, new AgentPolicy("conservative", false, 10, 50, [], []));
    }

    public async Task RunAsync(IBrowserContext context)
    {
        var validation = await _client.ValidateProfileAsync();
        if (validation is { Valid: false, Critical: true })
        {
            Console.WriteLine("Validacao do perfil falhou: " + string.Join("; ", validation.Issues.Select(i => i.ErrorMessage)));
            await _client.EventAsync(null, "run.aborted", $"Validacao do perfil falhou: {string.Join("; ", validation.Issues.Select(i => i.ErrorMessage))}");
            return;
        }

        await _resolver.LoadAsync();
        var policy = await _client.GetPolicyAsync();
        if (policy is not null)
            _decision = new LlmDecisionEngine(_llm ?? new LlmClient(_client, false), _resolver, policy);
        _activeLearnings = await _client.GetActiveLearningsAsync();
        if (_activeLearnings.Count > 0)
            Console.WriteLine($"[Learning] Loaded {_activeLearnings.Count} approved learning(s).");

        // Busca credenciais de plataforma da API
        _platformCredentials = await _client.GetPlatformCredentialsAsync() ?? [];
        foreach (var credential in _platformCredentials)
            Console.WriteLine($"[Credentials] {credential.Platform} credential loaded for {credential.AccountEmail}");

        var applied = 0;
        var processed = 0;
        Guid specificTaskId = Guid.Empty;
        var specificTaskMode = !string.IsNullOrWhiteSpace(_options.TaskId) && Guid.TryParse(_options.TaskId, out specificTaskId);
        AgentTaskResponse? task = null;
        
        // Se um TaskId específico foi informado, busca apenas ele
        if (specificTaskMode)
        {
            Console.WriteLine($"[Run] Buscando task específica: {specificTaskId}");
            task = await _client.GetTaskAsync(specificTaskId);
            if (task is null)
            {
                Console.WriteLine($"Task {specificTaskId} não encontrada.");
                return;
            }
            _resolver.SetProfile(task.Profile, task.ResolveResumePath());
            var result = await ProcessSingleTaskSafelyAsync(context, task, policy);
            if (result) applied++;
            Console.WriteLine($"Parado apos processar task específica. Aplicadas: {applied}");
            await _client.EventAsync(null, "run.completed", $"Task específica processada. Aplicadas: {applied}");
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
        var validation = await _client.ValidateProfileAsync();
        if (validation is { Valid: false, Critical: true })
        {
            Console.WriteLine("Validacao do perfil falhou: " + string.Join("; ", validation.Issues.Select(i => i.ErrorMessage)));
            await _client.EventAsync(null, "run.aborted", $"Validacao do perfil falhou: {string.Join("; ", validation.Issues.Select(i => i.ErrorMessage))}");
            return;
        }

        await _resolver.LoadAsync();
        var policy = await _client.GetPolicyAsync();
        if (policy is not null)
            _decision = new LlmDecisionEngine(_llm ?? new LlmClient(_client, false), _resolver, policy);
        _activeLearnings = await _client.GetActiveLearningsAsync();
        _platformCredentials = await _client.GetPlatformCredentialsAsync() ?? [];

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

    private async Task<(PageObservation Observation, ApplicationSurfaceOpenResult? Failure)> EnsureLinkedInExpectedJobInExtensionAsync(
        BrowserExtensionHttpTransport transport,
        AgentTaskResponse task,
        string targetUrl,
        PageObservation observation)
    {
        var expectedJobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        if (string.IsNullOrWhiteSpace(expectedJobId))
            return (observation, null);

        var displayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(observation.Url);
        if (string.IsNullOrWhiteSpace(displayedJobId) || displayedJobId == expectedJobId)
            return (observation, null);

        await _client.EventAsync(task.Task.Id, "linkedin.job_mismatch_retry",
            $"expected={expectedJobId}; displayed={displayedJobId}; observedUrl={observation.Url}; retryUrl={targetUrl}");
        await transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: targetUrl, ReasonCode: "pre_apply.job_mismatch_retry"));
        await Task.Delay(2500);
        var retryObservation = await transport.ObserveAsync();
        var retryDisplayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(retryObservation.Url);
        if (string.IsNullOrWhiteSpace(retryDisplayedJobId) || retryDisplayedJobId == expectedJobId)
            return (retryObservation, null);

        var referenceJobId = LinkedInApplicationFlow.ExtractReferenceJobId(retryObservation.Url);
        var detail = referenceJobId == expectedJobId
            ? $"LinkedIn redirected from expected job {expectedJobId} to similar job {retryDisplayedJobId}; JobPilot stopped to avoid applying to the wrong vacancy."
            : $"LinkedIn opened job {retryDisplayedJobId}, but JobPilot expected {expectedJobId}.";

        return (retryObservation, new ApplicationSurfaceOpenResult(
            false,
            "job_mismatch",
            "needs_review",
            "Vaga",
            detail,
            FinalUrl: retryObservation.Url));
    }

    private async Task<ApplicationSurfaceOpenResult> OpenLinkedInSurfaceWithExtensionAsync(BrowserExtensionHttpTransport transport, AgentTaskResponse task, PageObservation initialObservation)
    {
        var jobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        try
        {
            var result = await transport.ExecuteAsync(new BrowserExtensionCommand(
                "open_apply_surface",
                ElementId: jobId ?? "",
                Value: task.Lead.Title,
                ReasonCode: "pre_apply.open_surface"));

            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            var ok = ReadBool(root, "ok", true);
            var opened = ReadBool(root, "opened", false);
            var surfaceKind = ReadString(root, "surfaceKind");
            var finalUrl = ReadString(root, "finalUrl");
            var detail = ReadString(root, "detail");
            var failureCode = ReadString(root, "failureCode");
            var failureState = ReadString(root, "failureState");
            var fieldLabel = ReadString(root, "fieldLabel");
            var error = ReadString(root, "error");

            if (BuildLinkedInJobMismatchSurface(jobId, finalUrl) is { } mismatchSurface)
                return mismatchSurface;

            if (!ok && (error.Contains("unsupported_command", StringComparison.OrdinalIgnoreCase) ||
                        error.Contains("element_not_found", StringComparison.OrdinalIgnoreCase)))
            {
                await _client.EventAsync(task.Task.Id, "extension.open_surface_fallback", $"Using observed elements because extension content script does not expose open_apply_surface yet. error={error}");
                return await OpenLinkedInSurfaceFromObservedElementsAsync(transport, task, initialObservation);
            }

            if (!ok || !opened)
            {
                if (failureCode == "easy_apply_modal_not_opened")
                {
                    var navigationFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, finalUrl, jobId);
                    if (navigationFallback.Opened)
                        return navigationFallback;
                    if (navigationFallback.FailureCode == "job_mismatch")
                        return navigationFallback;
                }

                return new ApplicationSurfaceOpenResult(
                    false,
                    string.IsNullOrWhiteSpace(failureCode) ? "application_surface_not_found" : failureCode,
                    string.IsNullOrWhiteSpace(failureState) ? "needs_review" : failureState,
                    string.IsNullOrWhiteSpace(fieldLabel) ? "Candidatar-se" : fieldLabel,
                    string.IsNullOrWhiteSpace(detail) ? "The extension could not open a LinkedIn application surface." : detail,
                    surfaceKind,
                    string.IsNullOrWhiteSpace(finalUrl) ? initialObservation.Url : finalUrl);
            }

            var suggestedPlatform = surfaceKind switch
            {
                "linkedin_easy_apply" => "LinkedIn",
                "inhire" => "InHire",
                "unsupported_external" => "Unsupported",
                _ => "LinkedIn"
            };

            return ApplicationSurfaceOpenResult.OpenedSurface(
                surfaceKind,
                suggestedPlatform,
                string.IsNullOrWhiteSpace(finalUrl) ? initialObservation.Url : finalUrl,
                detail,
                null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _client.EventAsync(task.Task.Id, "extension.open_surface_fallback", $"Using observed elements because extension content script does not expose open_apply_surface yet. error={ex.Message}");
            try
            {
                return await OpenLinkedInSurfaceFromObservedElementsAsync(transport, task, initialObservation);
            }
            catch (Exception fallbackEx) when (fallbackEx is not OperationCanceledException)
            {
                return new ApplicationSurfaceOpenResult(
                    false,
                    "easy_apply_modal_not_opened",
                    "needs_review",
                    "Candidatura simplificada",
                    $"The extension could not open LinkedIn Easy Apply. fallback_error={fallbackEx.Message}",
                    "linkedin_easy_apply",
                    initialObservation.Url);
            }
        }
    }

    private static async Task<ApplicationSurfaceOpenResult> TryOpenLinkedInEasyApplyByHrefAsync(BrowserExtensionHttpTransport transport, PageObservation observation, string fallbackUrl, string? expectedJobId)
    {
        try
        {
            return await OpenLinkedInEasyApplyByHrefAsync(transport, observation, fallbackUrl, expectedJobId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "easy_apply_modal_not_opened",
                "needs_review",
                "Candidatura simplificada",
                $"Easy Apply href fallback failed. error={ex.Message}",
                "linkedin_easy_apply",
                string.IsNullOrWhiteSpace(fallbackUrl) ? observation.Url : fallbackUrl);
        }
    }

    private static async Task<ApplicationSurfaceOpenResult> OpenLinkedInEasyApplyByHrefAsync(BrowserExtensionHttpTransport transport, PageObservation observation, string fallbackUrl, string? expectedJobId)
    {
        var easyApplyLink = observation.Elements.FirstOrDefault(e =>
            e.IsButtonLike &&
            !string.IsNullOrWhiteSpace(e.Href) &&
            e.Href.Contains("/apply/", StringComparison.OrdinalIgnoreCase) &&
            (e.Href.Contains("openSDUIApplyFlow", StringComparison.OrdinalIgnoreCase) ||
             AgentText.ContainsAny(e.SearchText, ["easy apply", "candidatura simplificada"])));

        if (easyApplyLink is null)
            return new ApplicationSurfaceOpenResult(false);

        await transport.ExecuteAsync(new BrowserExtensionCommand("navigate", Value: easyApplyLink.Href, ReasonCode: "pre_apply.easy_apply_href"));
        await Task.Delay(2500);
        var afterNavigation = await transport.ObserveAsync();
        if (BuildLinkedInJobMismatchSurface(expectedJobId, afterNavigation.Url) is { } mismatchSurface)
            return mismatchSurface;

        if (LooksLikeEasyApplyModal(afterNavigation))
        {
            return ApplicationSurfaceOpenResult.OpenedSurface(
                "linkedin_easy_apply",
                "LinkedIn",
                afterNavigation.Url,
                "LinkedIn Easy Apply modal opened by navigating to the apply href.");
        }

        return new ApplicationSurfaceOpenResult(
            false,
            "easy_apply_modal_not_opened",
            "needs_review",
            easyApplyLink.Label,
            "Easy Apply href navigation ran, but the modal did not open.",
            "linkedin_easy_apply",
            string.IsNullOrWhiteSpace(afterNavigation.Url) ? fallbackUrl : afterNavigation.Url);
    }

    private static ApplicationSurfaceOpenResult? BuildLinkedInJobMismatchSurface(string? expectedJobId, string observedUrl)
    {
        if (string.IsNullOrWhiteSpace(expectedJobId))
            return null;

        var displayedJobId = LinkedInApplicationFlow.ExtractDisplayedJobId(observedUrl);
        if (string.IsNullOrWhiteSpace(displayedJobId) || displayedJobId == expectedJobId)
            return null;

        var referenceJobId = LinkedInApplicationFlow.ExtractReferenceJobId(observedUrl);
        var detail = referenceJobId == expectedJobId
            ? $"LinkedIn redirected from expected job {expectedJobId} to similar job {displayedJobId}; JobPilot stopped to avoid applying to the wrong vacancy."
            : $"LinkedIn opened job {displayedJobId}, but JobPilot expected {expectedJobId}.";

        return new ApplicationSurfaceOpenResult(
            false,
            "job_mismatch",
            "needs_review",
            "Vaga",
            detail,
            FinalUrl: observedUrl);
    }

    private async Task<ApplicationSurfaceOpenResult> OpenLinkedInSurfaceFromObservedElementsAsync(BrowserExtensionHttpTransport transport, AgentTaskResponse task, PageObservation initialObservation)
    {
        var jobId = LinkedInApplicationFlow.ExtractJobId(task.Lead.Url);
        var observedJobId = LinkedInApplicationFlow.ExtractJobId(initialObservation.Url);
        if (!string.IsNullOrWhiteSpace(jobId) &&
            !string.IsNullOrWhiteSpace(observedJobId) &&
            !jobId.Equals(observedJobId, StringComparison.OrdinalIgnoreCase))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "job_mismatch",
                "needs_review",
                "Vaga",
                $"LinkedIn opened job {observedJobId}, but JobPilot expected {jobId}.",
                FinalUrl: initialObservation.Url);
        }

        if (!TitleLooksVisible(initialObservation, task.Lead.Title))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "job_title_not_visible",
                "needs_review",
                "Vaga",
                "The expected job title is not visible in the LinkedIn page opened by the extension.",
                FinalUrl: initialObservation.Url);
        }

        if (LinkedInPageShowsAlreadyApplied(initialObservation))
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "already_applied",
                "applied",
                "Candidatura",
                "LinkedIn indicates this job was already applied.",
                "already_applied",
                initialObservation.Url);
        }

        var alreadyApplied = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && AgentText.ContainsAny(e.SearchText, ["applied", "candidatura enviada", "voce se candidatou", "ja se candidatou"]));
        if (alreadyApplied is not null)
        {
            return new ApplicationSurfaceOpenResult(
                false,
                "already_applied",
                "applied",
                "Candidatura",
                "LinkedIn indicates this job was already applied.",
                "already_applied",
                initialObservation.Url);
        }

        var easyApply = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && AgentText.ContainsAny(e.SearchText, ["easy apply", "candidatura simplificada", "candidate-se simplificadamente"]));
        if (easyApply is not null)
        {
            PageObservation afterClick;
            try
            {
                await transport.DispatchAsync(new BrowserExtensionCommand("click", easyApply.ElementId, ReasonCode: "pre_apply.easy_apply"));
                await Task.Delay(2500);
                afterClick = await transport.ObserveAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var clickFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, initialObservation.Url, jobId);
                if (clickFallback.Opened)
                    return clickFallback;
                if (clickFallback.FailureCode == "job_mismatch")
                    return clickFallback;

                return new ApplicationSurfaceOpenResult(
                    false,
                    "easy_apply_modal_not_opened",
                    "needs_review",
                    easyApply.Label,
                    $"Easy Apply was present, but the extension could not click it. error={ex.Message}",
                    "linkedin_easy_apply",
                    initialObservation.Url);
            }

            if (LooksLikeEasyApplyModal(afterClick))
            {
                return ApplicationSurfaceOpenResult.OpenedSurface(
                    "linkedin_easy_apply",
                    "LinkedIn",
                    afterClick.Url,
                    "LinkedIn Easy Apply modal opened via observed extension elements.");
            }

            var hrefFallback = await TryOpenLinkedInEasyApplyByHrefAsync(transport, initialObservation, afterClick.Url, jobId);
            if (hrefFallback.Opened)
                return hrefFallback;
            if (hrefFallback.FailureCode == "job_mismatch")
                return hrefFallback;

            return new ApplicationSurfaceOpenResult(
                false,
                "easy_apply_modal_not_opened",
                "needs_review",
                easyApply.Label,
                "Easy Apply was present, but the modal did not open.",
                "linkedin_easy_apply",
                afterClick.Url);
        }

        var externalApply = initialObservation.Elements.FirstOrDefault(e =>
            e.IsButtonLike && LooksLikeLinkedInExternalApply(e));
        if (externalApply is not null)
        {
            await transport.DispatchAsync(new BrowserExtensionCommand("click", externalApply.ElementId, ReasonCode: "pre_apply.external_apply"));
            await Task.Delay(3500);
            var afterClick = await transport.ObserveAsync();
            var finalUrl = string.IsNullOrWhiteSpace(afterClick.Url) ? externalApply.Href : afterClick.Url;
            var surfaceKind = finalUrl.Contains("inhire.app", StringComparison.OrdinalIgnoreCase) ? "inhire" : "unsupported_external";
            return ApplicationSurfaceOpenResult.OpenedSurface(
                surfaceKind,
                surfaceKind == "inhire" ? "InHire" : "Unsupported",
                finalUrl,
                surfaceKind == "inhire"
                    ? "LinkedIn external apply opened an InHire surface via observed extension elements."
                    : "LinkedIn job uses an external application site that JobPilot does not automate yet.");
        }

        return new ApplicationSurfaceOpenResult(
            false,
            "application_surface_not_found",
            "needs_review",
            "Candidatar-se",
            "No Easy Apply or external apply button was found in the LinkedIn job detail panel.",
            FinalUrl: initialObservation.Url);
    }

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

                // Confirmação
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

                // Atualiza state machine com a ação executada
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

                    // Self-healing: se é retriável, tenta continuar em vez de parar
                    if (recoveryAfterExecution.ContinueLoop && recoveryAfterExecution.Mode is RecoveryMode.Retry or RecoveryMode.Reobserve or RecoveryMode.Remap or RecoveryMode.Scroll)
                    {
                        await _client.EventAsync(task.Task.Id, "runtime.recovery",
                            $"Action failed; recovery={recoveryAfterExecution.Mode}; detail={recoveryAfterExecution.Reason}. Retry {session.RetryCount}/3.");
                        continue; // Tenta o próximo ciclo do loop
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
            "candidatura enviada há",
            "voce se candidatou",
            "você se candidatou",
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

    private Task<ActionExecutionResult> ExecuteApprovedActionAsync(IBrowserDriver driver, IPage? page, PageObservation observation, ApprovedAction approvedAction, AgentTaskResponse task, SessionContext session) =>
        ExecuteActionAsync(driver, page, observation, approvedAction.ToBrowserAction(), task, session);

    private async Task<ActionExecutionResult> ExecuteActionAsync(IBrowserDriver driver, IPage? page, PageObservation observation, BrowserAction action, AgentTaskResponse task, SessionContext session)
    {
        if (action.Type == "scroll")
        {
            await driver.ScrollAsync();
            return ActionExecutionResult.Ok("Scrolled.");
        }
        if (action.Type == "wait")
        {
            if (driver is BrowserExtensionDriver)
                await Task.Delay(1500);
            else if (page is not null)
                await page.WaitForTimeoutAsync(1500);
            else
                return ActionExecutionResult.Fail("browser_page_missing", "Playwright page is required for this browser driver.");
            return ActionExecutionResult.Ok("Waited.");
        }

        var element = observation.Elements.FirstOrDefault(e => e.ElementId == action.ElementId);
        if (element is null)
            return ActionExecutionResult.Fail("element_not_observed", "Action target was not in the current DOM observation.");

        var extensionDriver = driver is BrowserExtensionDriver;
        var playwrightPage = page;
        if (!extensionDriver && playwrightPage is null)
            return ActionExecutionResult.Fail("browser_page_missing", "Playwright page is required for this browser driver.");
        var locator = extensionDriver ? null : playwrightPage!.Locator($"[data-jobpilot-id='{action.ElementId}']:visible").First;
        try
        {
            switch (action.Type)
            {
                case "fill":
                    var valueSource = ResolveValueSource(element.SearchText, task, action.Reason);
                    if (RequiresExplicitSource(element, valueSource))
                        return ActionExecutionResult.Fail("sensitive_answer_missing_source", "Sensitive field requires an explicit profile or reusable answer source.");

                    await driver.FillAsync(action.ElementId, action.Value);

                    // === SELF-HEALING: validar pós-preenchimento ===
                    var validation = extensionDriver
                        ? ActionExecutionResult.Ok("Filled via browser extension.")
                        : await _postActionValidator.ValidateFillAsync(playwrightPage!, locator!, element, action.Value, session);
                    if (!validation.Success)
                    {
                        return validation;
                    }

                    var storedValue = RedactIfSensitive(element, action.Value);
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, element.TypeOrTag, valueSource, action.Confidence));
                    await _client.ApplicationAnswerAsync(new ApplicationAnswerRequest(task.Task.Id, element.Label, storedValue, valueSource, "local-resolver", action.Confidence));
                    session.Record(StableFieldKey(element.SearchText), action.Value, SemanticFieldType.Unknown);
                    if (valueSource == "ai-draft")
                        await RegisterAnswerReviewAsync(task.Task.Id, element, storedValue, action.Confidence);
                    break;
                case "select":
                    await driver.SelectAsync(action.ElementId, action.Value);
                    var selectionValidation = extensionDriver
                        ? ActionExecutionResult.Ok("Selected via browser extension.")
                        : await _postActionValidator.ValidateSelectionAsync(playwrightPage!, locator!, element, action.Value);
                    if (!selectionValidation.Success)
                        return selectionValidation;
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "select", "observed-selection", action.Confidence));
                    break;
                case "check":
                    await driver.CheckAsync(action.ElementId);
                    if (!extensionDriver)
                    {
                        var checkedState = await locator!.IsCheckedAsync(new LocatorIsCheckedOptions { Timeout = 2000 });
                        if (!checkedState)
                            return ActionExecutionResult.Fail("check_not_applied", "Checkbox/radio was not checked after action.");
                    }
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "checkbox", "observed-check", action.Confidence));
                    break;
                case "upload_file":
                    var resumePath = task.ResolveResumePath();
                    if (ResumeAlreadySelected(observation))
                        return ActionExecutionResult.Ok("Resume already selected.");
                    if (extensionDriver)
                        return ActionExecutionResult.Fail("resume_upload_requires_manual_selection", ManualResumeUploadMessage(resumePath));
                    if (string.IsNullOrWhiteSpace(resumePath) || !File.Exists(resumePath))
                        return ActionExecutionResult.Fail("resume_file_missing", $"Configured resume file was not found at {resumePath}.");
                    Console.WriteLine($"[UPLOAD] Procurando inputs file... encontrados={observation.Elements.Count(e => e.IsFile)}; alvo={element.Label}");
                    await driver.UploadFileAsync(action.ElementId, resumePath);
                    if (!extensionDriver && !await AnyFileInputHasFileAsync(playwrightPage!))
                        return ActionExecutionResult.Fail("resume_upload_not_confirmed", "Resume upload action ran, but no file input reports an attached file.");
                    await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "file", "resume-path", action.Confidence));
                    break;
                case "click":
                case "submit":
                    if (extensionDriver && action.Type == "click" && LooksLikeUploadTrigger(action, element) && !ResumeAlreadySelected(observation))
                        return ActionExecutionResult.Fail("resume_upload_requires_manual_selection", ManualResumeUploadMessage(task.ResolveResumePath()));

                    if (!extensionDriver && action.Type == "click" && LooksLikeUploadTrigger(action, element))
                    {
                        var fallbackResumePath = task.ResolveResumePath();
                        if (ResumeAlreadySelected(observation))
                            return ActionExecutionResult.Ok("Resume already selected.");
                        if (string.IsNullOrWhiteSpace(fallbackResumePath) || !File.Exists(fallbackResumePath))
                            return ActionExecutionResult.Fail("resume_file_missing", $"Configured resume file was not found at {fallbackResumePath}.");

                        var chooserAttached = await AttachResumeUsingFileChooserAsync(playwrightPage!, action.ElementId, fallbackResumePath);
                        if (!chooserAttached)
                            return ActionExecutionResult.Fail("resume_filechooser_failed", "Upload button did not expose a Playwright file chooser.");

                        await _client.ApplicationFieldAsync(new ApplicationFieldRequest(task.Task.Id, StableFieldKey(element.SearchText), element.Label, "file", "resume-path-filechooser", action.Confidence));
                        break;
                    }

                    if (BrowserActionGuard.BlocksNavigation(observation, action, out var blockedReason))
                        return ActionExecutionResult.Fail("required_fields_block_navigation", blockedReason);

                    if (!extensionDriver && action.Type == "click" && element.Href.Contains("/apply/", StringComparison.OrdinalIgnoreCase))
                        await NavigateToApplyAsync(playwrightPage!, element.Href);
                    else
                    {
                        try { await driver.ClickAsync(action.ElementId); }
                        catch when (!string.IsNullOrWhiteSpace(element.Href) && action.Type == "click")
                        { await NavigateToApplyAsync(playwrightPage!, element.Href); }
                    }
                    if (!extensionDriver)
                    {
                        var navigationValidation = await _postActionValidator.ValidateNavigationActionAsync(playwrightPage!, observation, action);
                        if (!navigationValidation.Success)
                            return navigationValidation;
                    }
                    break;
                default:
                    return ActionExecutionResult.Fail("unsupported_action", $"Unsupported action '{action.Type}'.");
            }
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Fail("action_failed", ex.Message);
        }

        return ActionExecutionResult.Ok("Executed.");
    }

    private static async Task<bool> AttachResumeUsingFileChooserAsync(IPage page, string elementId, string resumePath)
    {
        try
        {
            Console.WriteLine($"[UPLOAD] Sem input file planejado; tentando fallback filechooser no botao: element={elementId}; file={Path.GetFileName(resumePath)}");
            var uploadButton = page.Locator($"[data-jobpilot-id='{elementId}']:visible").First;
            var fileChooser = await page.RunAndWaitForFileChooserAsync(
                async () => await uploadButton.ClickAsync(new LocatorClickOptions { Timeout = 6000 }),
                new PageRunAndWaitForFileChooserOptions { Timeout = 5000 });

            await fileChooser.SetFilesAsync(resumePath);
            await page.WaitForTimeoutAsync(600);
            var attached = await AnyFileInputHasFileAsync(page);
            Console.WriteLine($"[UPLOAD] Filechooser processado: attached={attached}");
            return attached || await PageShowsFileNameAsync(page, resumePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD] Falha no fallback filechooser: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> AnyFileInputHasFileAsync(IPage page)
    {
        try
        {
            var attachedCount = await page.Locator("input[type='file']").EvaluateAllAsync<int>(
                "els => els.reduce((sum, el) => sum + ((el.files && el.files.length) || 0), 0)");
            return attachedCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> PageShowsFileNameAsync(IPage page, string resumePath)
    {
        try
        {
            var fileName = Path.GetFileName(resumePath);
            var text = await PageObserver.GetVisibleTextAsync(page);
            return AgentText.ContainsAny(text, [fileName, ".pdf", ".doc", ".docx"]);
        }
        catch
        {
            return false;
        }
    }

    private static async Task NavigateToApplyAsync(IPage page, string href)
    {
        try
        {
            await page.GotoAsync(href, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 12000 });
        }
        catch when (page.Url.Contains("/apply/", StringComparison.OrdinalIgnoreCase) || page.Url.Contains("openSDUIApplyFlow", StringComparison.OrdinalIgnoreCase))
        {
            await page.WaitForTimeoutAsync(5000);
        }
    }

    private static async Task<IPage> SelectBestPageAsync(IPage current)
    {
        await current.WaitForTimeoutAsync(1800);
        var pages = current.Context.Pages.Where(p => !p.IsClosed).ToList();
        var external = pages.LastOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.Url) &&
            !p.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs-tracker", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs/view", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("linkedin.com/jobs/search", StringComparison.OrdinalIgnoreCase));

        var selected = external ?? pages.LastOrDefault(p =>
            !p.Url.Contains("jobs-tracker", StringComparison.OrdinalIgnoreCase) &&
            !p.Url.Contains("stage=clicked_apply", StringComparison.OrdinalIgnoreCase)) ?? current;

        if (selected != current)
        {
            await selected.BringToFrontAsync();
            try { await selected.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 8000 }); }
            catch { await selected.WaitForTimeoutAsync(1000); }
        }
        return selected;
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
            "arquivo foi carregado", "carregado com sucesso", "upload concluido", "upload concluído",
            "file uploaded", "uploaded successfully",
            "ultima utilizacao", "última utilização", "desmarcar selecao", "desmarcar seleção"
        ]);

    private static bool LooksLikeUploadTrigger(BrowserAction action, ElementCandidate element) =>
        element.IsButtonLike &&
        AgentText.ContainsAny($"{action.Label} {action.Reason} {element.SearchTextWithHints}", [
            "anexar curriculo", "anexar currículo", "enviar curriculo", "enviar currículo",
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
                $"Ação de fluxo bem-sucedida: {action.Type}",
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
            "Reconhecer confirmação de candidatura",
            "A tela atual indicou que a candidatura foi enviada com sucesso.",
            JsonSerializer.Serialize(new { observation.Url, text = observation.VisibleText[..Math.Min(observation.VisibleText.Length, 240)] }),
            JsonSerializer.Serialize(new { result = "mark_applied" }),
            JsonSerializer.Serialize(new { observation.Title, observation.HasModal }),
            "low"));
        if (learning is not null)
            await _client.LearningOutcomeAsync(new AgentLearningOutcomeRequest(learning.PatternKey, true, taskId, "Confirmation detected."));
    }
}
