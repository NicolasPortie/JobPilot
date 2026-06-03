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
        await _client.EventAsync(null, "discover.started", $"keywords={_options.Keywords}; location={_options.Location}; limit={_options.Limit}");
        var url = await _navigator.BuildSearchUrlAsync();

        if (string.Equals(_options.BrowserDriverMode, "extension", StringComparison.OrdinalIgnoreCase))
        {
            await DiscoverWithExtensionAsync(url);
            return;
        }

        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await page.WaitForTimeoutAsync(2500);

        if (await _navigator.NeedsLoginAsync(page))
        {
            if (!await _navigator.TryLoginAsync(page))
            {
                await _client.EventAsync(null, "discover.login_required", "LinkedIn login required.");
                Console.WriteLine("Login do LinkedIn necessario. Faca login manualmente no navegador aberto.");
                return;
            }
        }

        var jobs = new List<DiscoveredJobDto>();
        var scrolls = 0;
        var extracted = 0;
        var duplicates = 0;
        var maxScrolls = Math.Min(20, Math.Max(4, _options.Limit / 6));

        while (jobs.Count < _options.Limit && scrolls < maxScrolls)
        {
            var batch = await ExtractJobsAsync(page);
            extracted += batch.Count;
            foreach (var job in batch)
            {
                if (jobs.Any(j => j.Url == job.Url))
                {
                    duplicates++;
                    continue;
                }
                jobs.Add(job);
                if (jobs.Count >= _options.Limit) break;
            }

            await page.Mouse.WheelAsync(0, 1600);
            await page.WaitForTimeoutAsync(1800);
            scrolls++;
        }

        var evidence = await PageObserver.CaptureEvidenceAsync(page, "discover-completed");
        await _client.PostDiscoveredAsync(jobs);
        var autoQueue = await _client.AutoQueueAsync();
        await _client.EventAsync(null, "discover.completed", $"{jobs.Count} vagas do LinkedIn salvas; {autoQueue?.Queued ?? 0} automaticamente enfileiradas. extracted={extracted}; duplicates={duplicates}; scrolls={scrolls}; {evidence}");
        Console.WriteLine($"Discovery: {jobs.Count} vagas salvas; {autoQueue?.Queued ?? 0} enfileiradas. Extraidas={extracted}; duplicadas={duplicates}.");
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

    internal static bool PassesFilters(DiscoveredJobDto job, JobFilter? filter, string keywords)
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
        // Normaliza removendo pontos/espaços para lidar com "asp.net" vs "asp net" vs "aspnet".
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
            // Se só tem palavras genéricas, pelo menos uma deve estar no título
            if (!genericKeywords.Any(k => titleNoDiacritics.Contains(k)))
                return false;
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
              const params = new URLSearchParams(document.location.search);
              const pageText = document.body?.innerText || '';
              const remoteSearch = (params.get('f_WT') || '').split(',').includes('2') || /última semana\s+remoto|ultima semana\s+remoto|remote/i.test(pageText);
              const readCard = card => {
                const link = card.querySelector('a[href*="/jobs/view/"]');
                const title = cleanTitle(card.querySelector('.job-card-list__title, .base-search-card__title, .job-card-container__link, a[href*="/jobs/view/"]')?.textContent);
                const company = cleanText(card.querySelector('.job-card-container__primary-description, .base-search-card__subtitle, [data-test-job-card-company-name]')?.textContent);
                const jobLocation = cleanText(card.querySelector('.job-card-container__metadata-item, .job-search-card__location, [data-test-job-card-location]')?.textContent);
                const url = normalizeUrl(link?.getAttribute('href'));
                return { title, company, location: jobLocation || (remoteSearch ? 'Remote search filter' : ''), description: '', url, isRemote: remoteSearch || /remote|remoto|home office/i.test(`${title} ${jobLocation}`) };
              };
              const fromCards = Array.from(document.querySelectorAll('[data-job-id], .job-card-container, .jobs-search-results__list-item')).map(readCard);
              const fromLinks = Array.from(document.querySelectorAll('a[href*="/jobs/view/"]')).map(link => {
                const card = link.closest('[data-job-id], .job-card-container, .jobs-search-results__list-item, li, article') || link;
                const title = cleanTitle(link.textContent || card.textContent || '');
                const text = (card.textContent || '').trim();
                const lines = text.split('\n').map(x => x.trim()).filter(Boolean);
                const company = lines.find(x => x && x !== title && !/promoted|viewed|applicant|candidatar|candidate/i.test(x)) || '';
                const location = lines.find(x => /remote|remoto|brazil|brasil|sao paulo|rio de janeiro|curitiba|campinas|florianopolis|porto alegre/i.test(x)) || '';
                return { title, company, location: location || (remoteSearch ? 'Remote search filter' : ''), description: '', url: normalizeUrl(link.getAttribute('href')), isRemote: remoteSearch || /remote|remoto|home office/i.test(`${title} ${location}`) };
              });
              return [...fromCards, ...fromLinks].filter(j => j.title && j.url);
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
