using JobPilot.Agent.Browser;
using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class BrowserExtensionJobDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_NavigatesExtractsScrollsAndDeduplicatesJobs()
    {
        var transport = new FakeDiscoveryTransport();
        var discovery = new BrowserExtensionJobDiscovery(transport);

        var jobs = await discovery.DiscoverAsync("https://www.linkedin.com/jobs/search/?keywords=.NET", 2);

        Assert.Equal(["navigate", "extract_jobs", "observe", "scroll"], transport.Commands.Select(x => x.Type).Take(4));
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, job => job.Title == ".NET Developer" && job.IsRemote);
        Assert.Contains(jobs, job => job.Title == "QA Tester");
    }

    private sealed class FakeDiscoveryTransport : IBrowserExtensionTransport
    {
        private int _extractCount;
        public List<BrowserExtensionCommand> Commands { get; } = [];

        public Task<string> ExecuteAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command.Type == "navigate")
                return Task.FromResult("""{"ok":true,"url":"https://www.linkedin.com/jobs/search/"}""");

            if (command.Type == "extract_jobs")
            {
                _extractCount++;
                return Task.FromResult(_extractCount == 1
                    ? """
                      {"jobs":[
                        {"title":".NET Developer","company":"Acme","location":"Remote","description":"","url":"https://www.linkedin.com/jobs/view/1","isRemote":true},
                        {"title":".NET Developer","company":"Acme","location":"Remote","description":"","url":"https://www.linkedin.com/jobs/view/1","isRemote":true}
                      ]}
                      """
                    : """
                      {"jobs":[
                        {"title":"QA Tester","company":"Beta","location":"Brasil","description":"","url":"https://www.linkedin.com/jobs/view/2","isRemote":false}
                      ]}
                      """);
            }

            return Task.FromResult("""{"ok":true}""");
        }

        public Task<PageObservation> ObserveAsync(CancellationToken cancellationToken = default)
        {
            Commands.Add(new BrowserExtensionCommand("observe"));
            return Task.FromResult(new PageObservation("https://www.linkedin.com/jobs/search/", "LinkedIn", ".NET Developer .NET Developer Acme Remote QA Tester QA Tester Beta Brasil", []));
        }

        public Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default) =>
            Task.FromResult("");

        public Task DispatchAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }
    }
}
