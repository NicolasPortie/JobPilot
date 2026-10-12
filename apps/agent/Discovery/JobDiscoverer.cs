using System.Text.Json;
using JobPilot.Agent.Application;
using JobPilot.Agent.Browser;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;
using Microsoft.Playwright;

namespace JobPilot.Agent.Discovery;

public sealed class JobDiscoverer
{
    private readonly AgentOptions _options;
    private readonly JobPilotApiClient _client;
    private readonly LinkedInNavigator _navigator;

    public JobDiscoverer(AgentOptions options, JobPilotApiClient client, LinkedInNavigator navigator)
    {
        _options = options;
        _client = client;
        _navigator = navigator;
    }

    public async Task DiscoverAsync(IPage page)
    {
        var profilePath = string.IsNullOrWhiteSpace(_options.BrowserProfilePath) ? "(padrao)" : _options.BrowserProfilePath;
        await _client.EventAsync(null, "discover.started", $"keywords={_options.Keywords}; location={_options.Location}; limit={_options.Limit}; profile={profilePath}");
        var url = await _navigator.BuildSearchUrlAsync();
        Console.WriteLine($"[Discover] Perfil persistente: {profilePath}");

        if (string.Equals(_options.BrowserDriverMode, "extension", StringComparison.OrdinalIgnoreCase))
        {
            await DiscoverWithExtensionAsync(url);
            return;
        }

        if (await _navigator.HasPersistedSessionAsync(page))
        {
            Console.WriteLine("[Discover] Sessao LinkedIn detectada no perfil (cookie li_at); aquecendo antes da busca.");
            await page.GotoAsync("https://www.linkedin.com/feed/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.WaitForTimeoutAsync(2000);
        }

        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(2500);

        if (await IsSecurityChallengeAsync(page))
        {
            await FailWithChallengeAsync(page);
            return;
        }

        if (await _navigator.NeedsLoginAsync(page))
        {
            var (email, password) = await ResolveLinkedInCredentialAsync();
            var loggedIn = false;

            if (!string.IsNullOrWhiteSpace(password))
            {
                loggedIn = await _navigator.TryLoginAsync(page, email, password);
            }
            else if (await _navigator.HasPersistedSessionAsync(page))
            {
                Console.WriteLine("[Discover] Credencial salva sem senha automatica; reutilizando sessao persistida do perfil.");
                loggedIn = await _navigator.TryRestorePersistedSessionAsync(page, url);
            }

            if (!loggedIn)
            {
                if (await IsSecurityChallengeAsync(page))
                {
                    await FailWithChallengeAsync(page);
                    return;
                }

                const string detail = "Login do LinkedIn necessario. A sessao persistida expirou ou o agente nao conseguiu carregar a credencial salva. Verifique Configuracoes > Credenciais do LinkedIn ou aceite o login uma vez no perfil dedicado.";
                await _client.EventAsync(null, "discover.login_required", detail);
                Console.Error.WriteLine(detail);
                Environment.ExitCode = 1;
                return;
            }

            // Reabre a busca apos o login (o login redireciona para o feed).
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.WaitForTimeoutAsync(2500);
        }
        else
        {
            Console.WriteLine("[Discover] Ja autenticado; seguindo com a busca sem novo login.");
        }

        await WaitForResultsAsync(page);

        var filter = await _client.GetJobFilterAsync();

        var jobs = new List<DiscoveredJobDto>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTitleCompany = new HashSet<string>(StringComparer.Ordinal);
        var scrolls = 0;
        var extracted = 0;
        var duplicates = 0;
        var irrelevant = 0;
        var stagnant = 0;
        // Mais scrolls em headless: a lista do LinkedIn carrega por lazy load conforme o scroll.
        var maxScrolls = Math.Min(40, Math.Max(8, _options.Limit / 3));

        while (jobs.Count < _options.Limit && scrolls < maxScrolls && stagnant < 4)
        {
            var batch = await ExtractJobsAsync(page);
            extracted += batch.Count;
            var before = jobs.Count;
            foreach (var job in batch)
            {
                // Dedup forte: por URL canônica E por (título normalizado + empresa).
                var urlKey = CanonicalUrl(job.Url);
                var titleCompanyKey = TitleCompanyKey(job);
                if (!seenUrls.Add(urlKey) || !seenTitleCompany.Add(titleCompanyKey))
                {
                    duplicates++;
                    continue;
                }

                // Relevância: descarta vagas fora do escopo da busca (BUG: PassesFilters existia mas nunca era chamado).
                if (!PassesFilters(job, filter, _options.Keywords, trustPlatformSearch: true))
                {
                    irrelevant++;
                    continue;
                }

                jobs.Add(job);
                if (jobs.Count >= _options.Limit) break;
            }

            // Para quando varios scrolls seguidos nao trazem vagas novas (fim da lista).
            stagnant = jobs.Count == before ? stagnant + 1 : 0;
            if (jobs.Count >= _options.Limit) break;

            await ScrollResultsAsync(page);
            await page.WaitForTimeoutAsync(1600);
            scrolls++;
        }

        var evidence = await PageObserver.CaptureEvidenceAsync(page, "discover-completed");

        if (jobs.Count == 0)
        {
            var detail = $"Nenhuma vaga extraida (extracted={extracted}; irrelevant={irrelevant}; duplicates={duplicates}; scrolls={scrolls}). Possivel bloqueio anti-bot, sessao deslogada ou filtros muito restritivos. {evidence}";
            await _client.EventAsync(null, "discover.empty", detail);
            Console.Error.WriteLine(detail);
            Environment.ExitCode = 1;
            return;
        }

        await _client.PostDiscoveredAsync(jobs);
        var autoQueue = await _client.AutoQueueAsync();
        await _client.EventAsync(null, "discover.completed", $"{jobs.Count} vagas do LinkedIn salvas; {autoQueue?.Queued ?? 0} automaticamente enfileiradas. extracted={extracted}; duplicates={duplicates}; irrelevant={irrelevant}; scrolls={scrolls}; {evidence}");
        Console.WriteLine($"Discovery: {jobs.Count} vagas salvas; {autoQueue?.Queued ?? 0} enfileiradas. Extraidas={extracted}; duplicadas={duplicates}; irrelevantes={irrelevant}.");
    }

    private async Task<(string? Email, string? Password)> ResolveLinkedInCredentialAsync()
    {
        try
        {
            var credentials = await _client.GetPlatformCredentialsAsync();
            if (credentials is null)
            {
                Console.WriteLine("[Discover] Falha ao consultar credenciais salvas (API indisponivel ou HTTP nao autorizado). Tentando sessao persistida ou variaveis de ambiente.");
                return (null, null);
            }

            var linkedInCredentials = credentials
                .Where(c => c.Platform.Contains("linkedin", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(c.AccountEmail))
                .ToList();

            if (linkedInCredentials.Count == 0)
            {
                Console.WriteLine("[Discover] Nenhuma credencial ativa do LinkedIn retornada pela API.");
                return (null, null);
            }

            var credential = SelectLinkedInCredential(credentials);
            if (credential is null)
                return (null, null);

            if (string.IsNullOrWhiteSpace(credential.Password))
            {
                Console.WriteLine($"[Discover] Credencial do LinkedIn encontrada para {credential.AccountEmail}, mas a senha nao esta disponivel para login automatico. Tentando reutilizar sessao persistida do perfil.");
                return (credential.AccountEmail, null);
            }

            Console.WriteLine($"[Discover] Credencial do LinkedIn carregada para {credential.AccountEmail}.");
            return (credential.AccountEmail, credential.Password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Discover] Falha ao carregar credenciais salvas: {ex.Message}");
        }

        return (null, null);
    }

    // URL canônica do anúncio: prioriza o ID numérico do LinkedIn (/jobs/view/{id}) quando presente,
    // pois o mesmo anúncio aparece com sufixos/variações. Caso contrário, normaliza removendo query e barra final.
    internal static string CanonicalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        var value = url.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(value, @"/jobs/view/(\d+)");
        if (match.Success) return $"linkedin:{match.Groups[1].Value}";

        var queryIndex = value.IndexOf('?');
        if (queryIndex >= 0) value = value[..queryIndex];
        return value.TrimEnd('/').ToLowerInvariant();
    }

    // Chave de dedup por conteúdo: título normalizado (sem acentos/pontuação) + empresa normalizada.
    internal static string TitleCompanyKey(DiscoveredJobDto job)
    {
        static string Norm(string? s)
        {
            var value = AgentText.RemoveDiacritics(s ?? "").ToLowerInvariant();
            value = System.Text.RegularExpressions.Regex.Replace(value, @"[^a-z0-9]+", " ");
            return value.Trim();
        }
        return $"{Norm(job.Title)}|{Norm(job.Company)}";
    }

    internal static PlatformCredential? SelectLinkedInCredential(IEnumerable<PlatformCredential>? credentials)
    {
        var linkedIn = credentials?
            .Where(c =>
                c.Platform.Contains("linkedin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(c.AccountEmail))
            .ToList() ?? [];

        // A credential without a password is not a login credential. Persisted
        // browser sessions are handled separately by ResolveLinkedInCredentialAsync.
        return linkedIn.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Password));
    }

    private static readonly string[] ChallengeSignals =
        ["captcha", "i'm not a robot", "nao sou um robo", "puzzle", "desafio de seguranca", "verificacao de seguranca"];

    private static async Task<bool> IsSecurityChallengeAsync(IPage page)
    {
        if (page.Url.Contains("checkpoint/challenge", StringComparison.OrdinalIgnoreCase) ||
            page.Url.Contains("/checkpoint/", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var text = AgentText.RemoveDiacritics(await PageObserver.GetVisibleTextAsync(page)).ToLowerInvariant();
            return ChallengeSignals.Any(text.Contains);
        }
        catch
        {
            return false;
        }
    }

    private async Task FailWithChallengeAsync(IPage page)
    {
        var evidence = await PageObserver.CaptureEvidenceAsync(page, "discover-security-challenge");
        var detail = $"Desafio de seguranca do LinkedIn detectado durante a descoberta. Resolva manualmente logando uma vez no perfil dedicado e tente novamente. {evidence}";
        await _client.EventAsync(null, "discover.security_challenge", detail);
        Console.Error.WriteLine(detail);
        Environment.ExitCode = 1;
    }

    private static async Task WaitForResultsAsync(IPage page)
    {
        try
        {
            await page.WaitForSelectorAsync(
                "a[href*='/jobs/view/'], [data-job-id], .job-card-container, .jobs-search-results__list-item",
                new PageWaitForSelectorOptions { Timeout = 15000, State = WaitForSelectorState.Attached });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Discover] Lista de vagas nao apareceu no tempo esperado: {ex.Message}");
        }
    }

    private static async Task ScrollResultsAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync("""
                () => {
                  const containers = document.querySelectorAll(
                    '.jobs-search-results-list, div.jobs-search-results-list, .scaffold-layout__list, .scaffold-layout__list-detail');
                  containers.forEach(c => c.scrollBy(0, c.clientHeight || 1200));
                  window.scrollBy(0, 1600);
                  const buttons = Array.from(document.querySelectorAll('button'));
                  const showMore = buttons.find(b => /ver mais vagas|mostrar mais|see more jobs|show more/i.test((b.textContent || '').trim()));
                  if (showMore) showMore.click();
                }
            """);
        }
        catch
        {
            await page.Mouse.WheelAsync(0, 1600);
        }
    }

    private async Task DiscoverWithExtensionAsync(string url)
    {
        var discovery = new BrowserExtensionJobDiscovery(new BrowserExtensionHttpTransport(_client));
        var jobs = (await discovery.DiscoverAsync(url, _options.Limit)).ToList();
        await _client.PostDiscoveredAsync(jobs);
        var autoQueue = await _client.AutoQueueAsync();
        await _client.EventAsync(null, "discover.completed", $"{jobs.Count} vagas do LinkedIn salvas via extensao; {autoQueue?.Queued ?? 0} automaticamente enfileiradas.");
        Console.WriteLine($"Discovery via extensao: {jobs.Count} vagas salvas; {autoQueue?.Queued ?? 0} enfileiradas.");
    }

    private static readonly Dictionary<string, string[]> RoleSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["desenvolvedor"] = ["developer", "programmer", "programador", "engenheiro", "engineer"],
        ["developer"] = ["desenvolvedor", "programmer", "programador", "engenheiro", "engineer"],
        ["engenheiro"] = ["engineer", "desenvolvedor", "developer"],
        ["engineer"] = ["engenheiro", "desenvolvedor", "developer"],
        ["programador"] = ["programmer", "developer", "desenvolvedor"],
        ["programmer"] = ["programador", "developer", "desenvolvedor"],
        ["analista"] = ["analyst"],
        ["analyst"] = ["analista"],
    };

    private static readonly HashSet<string> GenericStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "remoto", "remote", "home office", "hibrido", "hybrid", "presencial", "onsite",
        "brasil", "brazil", "sao paulo", "rio de janeiro", "curitiba", "campinas",
        "florianopolis", "porto alegre", "belo horizonte", "brasilia", "salvador",
        "junior", "jr", "pleno", "pl", "senior", "sr", "estagio", "trainee", "intern",
        "entry", "associate", "mid", "lead", "manager", "director", "executive",
        "full time", "part time", "contract", "clt", "pj", "freelance", "temporario", "temporary"
    };

    private static readonly string[] TechAreaSignals =
    [
        "developer", "desenvolvedor", "engineer", "engenheiro", "programador", "programmer",
        "software", "dev", ".net", "java", "python", "javascript", "react", "angular",
        "backend", "frontend", "fullstack", "mobile", "cloud", "data", "dados", "analista",
        "analyst", "tester", "qa", "security", "support", "suporte", "infra", "sre", "ops",
        "aspnet", "asp.net", "c#", "csharp", "go", "golang", "ruby", "php", "node", "vue",
        "aws", "azure", "docker", "kubernetes", "sql", "database", "banco de dados", "bi",
        "machine learning", "inteligencia artificial", "ia", "blockchain", "web", "frontend"
    ];

    // Tecnologias/frameworks específicos (sem palavras genéricas de cargo como "desenvolvedor", "engineer")
    private static readonly string[] TechSpecificSignals =
    [
        ".net", "java", "python", "javascript", "react", "angular", "typescript",
        "backend", "frontend", "fullstack", "mobile", "cloud", "data", "dados",
        "aspnet", "asp.net", "c#", "csharp", "go", "golang", "ruby", "php", "node", "vue",
        "aws", "azure", "docker", "kubernetes", "sql", "database", "banco de dados", "bi",
        "machine learning", "inteligencia artificial", "ia", "blockchain", "web", "salesforce",
        "sap", "oracle", "postgres", "mysql", "mongo", "redis", "elasticsearch", "terraform",
        "qa", "tester", "selenium", "cypress", "jest", "junit", "cucumber", "playwright"
    ];

    private static readonly string[] NonTechTitles =
    [
        "vendas", "sales", "marketing", "executivo", "executive", "representante", "representative",
        "comercial", "commercial", "recrutador", "recruiter", "rh", "hr", "recursos humanos",
        "human resources", "administrativo", "administrative", "auxiliar", "assistant",
        "recepcionista", "receptionist", "operador", "operator", "motorista", "driver",
        "seguranca", "security guard", "limpeza", "cleaning", "cozinha", "kitchen",
        "garcom", "waiter", "caixa", "cashier", "telemarketing", "call center",
        "atendente", "attendant", "porteiro", "doorman", "entregador", "delivery",
        "promotor", "promoter", "estoque", "stock", "logistica", "logistics",
        "farmacia", "pharmacy", "enfermagem", "nursing", "medico", "doctor",
        "advogado", "lawyer", "contador", "accountant", "professor", "teacher"
    ];

    internal static bool PassesFilters(DiscoveredJobDto job, JobFilter? filter, string keywords, bool trustPlatformSearch = false)
    {
        if (filter is null) return true;

        var titleLower = job.Title.ToLowerInvariant();
        var locationLower = job.Location?.ToLowerInvariant() ?? "";
        var titleNoDiacritics = AgentText.RemoveDiacritics(titleLower);

        // Parse keywords da busca
        var keywordParts = keywords.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => k.Length > 1)
            .ToList();

        var substantiveKeywords = keywordParts.Where(k => !GenericStopwords.Contains(k)).ToList();
        var genericKeywords = keywordParts.Where(k => GenericStopwords.Contains(k)).ToList();

        // Na descoberta headless, o LinkedIn ja filtrou por keywords na URL — nao exigir novamente no titulo.
        if (!trustPlatformSearch)
        {
            // REGRA 1: Keywords substantivas DEVEM estar no título (não basta estar no local/empresa)
            if (substantiveKeywords.Count > 0)
            {
                var hasSubstantiveInTitle = substantiveKeywords.Any(k =>
                {
                    if (titleNoDiacritics.Contains(k)) return true;
                    if (RoleSynonyms.TryGetValue(k, out var syns))
                        return syns.Any(s => titleNoDiacritics.Contains(s));
                    return false;
                });
                if (!hasSubstantiveInTitle)
                    return false;
            }

            // REGRA 1b: Se há keywords técnicas específicas (linguagem/framework), exigir match bidirecional.
            static string NormTech(string s) => s.Replace(".", "").Replace(" ", "").Replace("#", "sharp").ToLowerInvariant();
            var normTitle = NormTech(titleNoDiacritics);
            var techSpecificKeywords = substantiveKeywords
                .Where(k => TechSpecificSignals.Any(t => NormTech(k).Contains(NormTech(t))))
                .ToList();
            if (techSpecificKeywords.Count > 0)
            {
                var matchingTechs = TechSpecificSignals.Where(t =>
                    normTitle.Contains(NormTech(t)) && techSpecificKeywords.Any(k => NormTech(k).Contains(NormTech(t)))).ToList();
                if (matchingTechs.Count == 0)
                    return false;
            }
            else if (genericKeywords.Count > 0)
            {
                if (!genericKeywords.Any(k => titleNoDiacritics.Contains(k)))
                    return false;
            }
        }

        // REGRA 2: Se a busca é de área tech, rejeitar vagas de outras áreas
        var isTechSearch = substantiveKeywords.Any(k => TechAreaSignals.Any(t => k.Contains(t)));
        if (isTechSearch && NonTechTitles.Any(nt => titleLower.Contains(nt)))
            return false;

        // REGRA 3: Filtros de senioridade e localidade
        if (filter.BlockedSeniorities.Any(s => titleNoDiacritics.Contains(AgentText.RemoveDiacritics(s.ToLowerInvariant())))) return false;
        if (filter.BlockedWorkplaceTypes.Any(w => locationLower.Contains(w.ToLowerInvariant()))) return false;
        if (!job.IsRemote && filter.AllowedWorkplaceTypes.Contains("remote")) return false;

        return true;
    }

    private static async Task<List<DiscoveredJobDto>> ExtractJobsAsync(IPage page)
    {
        var result = await page.EvaluateAsync<JsonElement>("""
            () => {
              const normalizeUrl = href => href ? new URL(href, document.location.href).toString().split('?')[0] : '';
              const cleanText = text => [...new Set((text || '').split('\n').map(x => x.trim()).filter(Boolean))].join(' ').trim();
              const cleanTitle = text => {
                let value = cleanText(text).replace(/\s+with verification$/i, '').trim();
                const half = Math.floor(value.length / 2);
                if (value.length % 2 === 0 && value.slice(0, half) === value.slice(half)) return value.slice(0, half).trim();
                const words = value.split(/\s+/);
                for (let size = Math.floor(words.length / 2); size >= 2; size--) {
                  const first = words.slice(0, size).join(' ');
                  const second = words.slice(size, size * 2).join(' ');
                  if (first.toLowerCase() === second.toLowerCase()) return first;
                }
                return value;
              };
              const isRemoteText = value => /\bremote\b|remoto|home office|trabalho remoto|100% remoto/i.test(value || '');
              // Rótulos de UI/filtro que NÃO são localidade real do card (evita o bug de "Remote search filter").
              const locationLabelNoise = /search filter|filtro|results|resultados|applicant|candidat|promoted|patrocinad|easy apply|candidatura simplificada|reposted|republicad|ago|atrás|há |week|sem(ana)?|day|dia|hour|hora|month|mês|mes/i;
              const looksLikeLocation = value => {
                if (!value) return false;
                const v = value.trim();
                if (v.length < 2 || v.length > 80) return false;
                if (locationLabelNoise.test(v)) return false;
                return /,|brasil|brazil|portugal|remoto|remote|híbrido|hibrido|presencial|on-?site|são paulo|sao paulo|rio de janeiro|belo horizonte|curitiba|campinas|florian|porto alegre|bras[ií]lia|salvador|recife|fortaleza|united states|estados unidos/i.test(v);
              };
              const pickLocation = card => {
                const selectors = [
                  '.job-card-container__metadata-item',
                  '.job-card-container__metadata-wrapper',
                  '.artdeco-entity-lockup__caption',
                  '.job-search-card__location',
                  '[data-test-job-card-location]'
                ];
                for (const sel of selectors) {
                  for (const el of card.querySelectorAll(sel)) {
                    const t = cleanText(el.textContent);
                    if (looksLikeLocation(t)) return t;
                  }
                }
                return '';
              };
              const readCard = card => {
                const link = card.querySelector('a[href*="/jobs/view/"]');
                const title = cleanTitle(card.querySelector('.job-card-list__title, .base-search-card__title, .job-card-container__link, a[href*="/jobs/view/"]')?.textContent);
                const company = cleanText(card.querySelector('.job-card-container__primary-description, .base-search-card__subtitle, [data-test-job-card-company-name]')?.textContent);
                const jobLocation = pickLocation(card);
                const url = normalizeUrl(link?.getAttribute('href'));
                return { title, company, location: jobLocation, description: '', url, isRemote: isRemoteText(`${title} ${jobLocation}`) };
              };
              const fromCards = Array.from(document.querySelectorAll('[data-job-id], .job-card-container, .jobs-search-results__list-item')).map(readCard);
              const fromLinks = Array.from(document.querySelectorAll('a[href*="/jobs/view/"]')).map(link => {
                const card = link.closest('[data-job-id], .job-card-container, .jobs-search-results__list-item, li, article') || link;
                const title = cleanTitle(link.textContent || card.textContent || '');
                const text = (card.textContent || '').trim();
                const lines = text.split('\n').map(x => x.trim()).filter(Boolean);
                const company = lines.find(x => x && x !== title && !/promoted|viewed|applicant|candidatar|candidate/i.test(x)) || '';
                const location = lines.find(looksLikeLocation) || '';
                return { title, company, location: location, description: '', url: normalizeUrl(link.getAttribute('href')), isRemote: isRemoteText(`${title} ${location}`) };
              });
              return [...fromCards, ...fromLinks]
                .filter(j => j.title && j.url)
                .reduce((acc, job) => {
                  const key = job.url || job.title;
                  if (!acc.seen.has(key)) { acc.seen.add(key); acc.items.push(job); }
                  return acc;
                }, { seen: new Set(), items: [] }).items;
            }
        """);

        if (result.ValueKind != JsonValueKind.Array) return [];

        var jobs = new List<DiscoveredJobDto>();
        foreach (var item in result.EnumerateArray())
        {
            var title = ReadString(item, "title");
            var url = ReadString(item, "url");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) continue;

            jobs.Add(new DiscoveredJobDto(
                title,
                ReadString(item, "company"),
                ReadString(item, "location"),
                ReadString(item, "description"),
                url,
                item.TryGetProperty("isRemote", out var isRemote) && isRemote.ValueKind == JsonValueKind.True));
        }
        return jobs;
    }

    private static string ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
