using JobPilot.Agent.Browser;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class BrowserExtensionDriverTests
{
    [Fact]
    public async Task BrowserExtensionDriver_DelegatesObservationAndActionsToTransport()
    {
        var transport = new FakeExtensionTransport();
        var driver = new BrowserExtensionDriver(transport);

        var observation = await driver.ObserveAsync();
        await driver.FillAsync("name", "Nicolas");
        await driver.ClickAsync("next");
        await driver.ScrollAsync();
        var screenshot = await driver.ScreenshotAsync("after");

        Assert.Equal("https://extension.test/apply", observation.Url);
        Assert.Equal("https://extension.test/apply", driver.CurrentUrl);
        Assert.Equal("extension://evidence/after.png", screenshot);
        Assert.Collection(
            transport.Commands,
            fill =>
            {
                Assert.Equal("fill", fill.Type);
                Assert.Equal("name", fill.ElementId);
                Assert.Equal("Nicolas", fill.Value);
            },
            click => Assert.Equal("click", click.Type),
            scroll => Assert.Equal("scroll", scroll.Type));
    }

    private sealed class FakeExtensionTransport : IBrowserExtensionTransport
    {
        public List<BrowserExtensionCommand> Commands { get; } = [];

        public Task<string> ExecuteAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult("""{"ok":true}""");
        }

        public Task<PageObservation> ObserveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PageObservation("https://extension.test/apply", "Apply", "", [
                new ElementCandidate("name", "input", "text", "", "Name", "", "", true, false, false, "", [], new ElementBounds(0, 0, 160, 32)),
                new ElementCandidate("next", "button", "", "button", "Next", "Next", "", false, false, false, "", [], new ElementBounds(0, 40, 100, 32))
            ]));

        public Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default) =>
            Task.FromResult($"extension://evidence/{reasonCode}.png");

        public async Task DispatchAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default) =>
            await ExecuteAsync(command, cancellationToken);
    }
}
