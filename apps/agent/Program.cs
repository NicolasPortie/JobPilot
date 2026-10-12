using JobPilot.Agent;
using JobPilot.Agent.Application;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Infrastructure;
using Microsoft.Playwright;

var options = AgentOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("""
    JobPilot.Agent

    discover --token TOKEN --keywords ".NET" --location Brazil
    run      --token TOKEN
    auto     --token TOKEN  (discover + apply in loop)

    Optional:
      --api http://localhost:5000
      --limit 25
      --headless true
      --continuous
      --interval-minutes 10
      --dry-run
      --browser-driver playwright|extension

    Environment:
      JOBPILOT_AGENT_TOKEN
      JOBPILOT_API_URL
      JOBPILOT_BROWSER_DRIVER=extension
      JOBPILOT_AUTO_SUBMIT=true
      JOBPILOT_AUTOPILOT_POLICY=conservative
      JOBPILOT_MAX_APPLICATIONS=10
      JOBPILOT_DRY_RUN=true
      LINKEDIN_LOGIN_WAIT_SECONDS=20
      JOBPILOT_BROWSER_CHANNEL=chrome
      JOBPILOT_BROWSER_PATH=C:\Program Files\Google\Chrome\Application\chrome.exe
      JOBPILOT_BROWSER_PROFILE=C:\Users\you\AppData\Local\JobPilot\browser-profile
      JOBPILOT_LLM_DECISIONS=true
      LINKEDIN_EMAIL
      LINKEDIN_PASSWORD
    """);
    return;
}

using var client = new JobPilotApiClient(options.ApiBaseUrl, options.Token);
var navigator = new LinkedInNavigator(options, client);
var resolver = new LocalAnswerResolver(client);
var usesExtension = options.BrowserDriverMode.Equals("extension", StringComparison.OrdinalIgnoreCase);

try
{
    if (options.Mode == "discover")
    {
        var discoverer = new JobDiscoverer(options, client, navigator);
        if (usesExtension)
        {
            if (options.Continuous)
            {
                while (true)
                {
                    await discoverer.DiscoverAsync(null!);
                    Console.WriteLine($"Discovery cycle completed. Waiting {options.DiscoveryIntervalMinutes} minute(s).");
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.DiscoveryIntervalMinutes)));
                }
            }
            else
            {
                await discoverer.DiscoverAsync(null!);
            }

            return;
        }

        using var lease = BrowserProfileLease.Acquire(options.BrowserProfilePath);
        using var playwright = await Playwright.CreateAsync();
        var context = await navigator.LaunchContextAsync(playwright);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

        if (options.Continuous)
        {
            while (true)
            {
                await discoverer.DiscoverAsync(page);
                Console.WriteLine($"Discovery cycle completed. Waiting {options.DiscoveryIntervalMinutes} minute(s).");
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.DiscoveryIntervalMinutes)));
            }
        }
        else
        {
            await discoverer.DiscoverAsync(page);
        }
    }
    else if (options.Mode == "run")
    {
        var llmEnabled = Environment.GetEnvironmentVariable("JOBPILOT_LLM_DECISIONS")?.ToLowerInvariant() != "false";
        var llmClient = new LlmClient(client, llmEnabled);
        var appAgent = new ApplicationAgent(options, client, resolver, navigator, llmClient);
        if (usesExtension)
        {
            await appAgent.RunExtensionOnlyAsync();
            return;
        }

        using var lease = BrowserProfileLease.Acquire(options.BrowserProfilePath);
        using var playwright = await Playwright.CreateAsync();
        var context = await navigator.LaunchContextAsync(playwright);
        await appAgent.RunAsync(context);
    }
    else if (options.Mode == "auto")
    {
        var llmEnabled = Environment.GetEnvironmentVariable("JOBPILOT_LLM_DECISIONS")?.ToLowerInvariant() != "false";
        var llmClient = new LlmClient(client, llmEnabled);
        var appAgent = new ApplicationAgent(options, client, resolver, navigator, llmClient);
        var discoverer = new JobDiscoverer(options, client, navigator);

        if (usesExtension)
        {
            while (true)
            {
                Console.WriteLine("[Auto] ===== Starting extension discovery cycle =====");
                await discoverer.DiscoverAsync(null!);

                Console.WriteLine("[Auto] Waiting 30s for backend to queue discovered jobs...");
                await Task.Delay(TimeSpan.FromSeconds(30));

                Console.WriteLine("[Auto] ===== Starting extension application cycle =====");
                await appAgent.RunExtensionOnlyAsync();

                Console.WriteLine($"[Auto] Cycle complete. Waiting {options.DiscoveryIntervalMinutes} minute(s) before next cycle.");
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.DiscoveryIntervalMinutes)));
            }
        }

        using var lease = BrowserProfileLease.Acquire(options.BrowserProfilePath);
        using var playwright = await Playwright.CreateAsync();
        var context = await navigator.LaunchContextAsync(playwright);

        while (true)
        {
            Console.WriteLine("[Auto] ===== Starting discovery cycle =====");
            var page = context.Pages.FirstOrDefault(p => !p.IsClosed) ?? await context.NewPageAsync();
            await discoverer.DiscoverAsync(page);

            Console.WriteLine("[Auto] Waiting 30s for backend to queue discovered jobs...");
            await Task.Delay(TimeSpan.FromSeconds(30));

            Console.WriteLine("[Auto] ===== Starting application cycle =====");
            await appAgent.RunAsync(context);

            Console.WriteLine($"[Auto] Cycle complete. Waiting {options.DiscoveryIntervalMinutes} minute(s) before next cycle.");
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.DiscoveryIntervalMinutes)));
        }
    }
    else
    {
        Console.WriteLine("Unknown mode. Use discover or run.");
    }
}
catch (InvalidOperationException ex) when (usesExtension && ex.Message.Contains("extension", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    Environment.ExitCode = 1;
}
