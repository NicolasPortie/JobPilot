using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Browser;

public sealed record BrowserExtensionCommand(string Type, string ElementId = "", string Value = "", string ReasonCode = "");

public interface IBrowserExtensionTransport
{
    Task<string> ExecuteAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default);
    Task<PageObservation> ObserveAsync(CancellationToken cancellationToken = default);
    Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default);
    Task DispatchAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default);
}

public sealed class BrowserExtensionDriver : IBrowserDriver
{
    private readonly IBrowserExtensionTransport _transport;
    private string _currentUrl = "";

    public BrowserExtensionDriver(IBrowserExtensionTransport transport) => _transport = transport;

    public string CurrentUrl => _currentUrl;

    public async Task<PageObservation> ObserveAsync()
    {
        var observation = await _transport.ObserveAsync();
        _currentUrl = observation.Url;
        return observation;
    }

    public Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default) =>
        _transport.ScreenshotAsync(reasonCode, cancellationToken);

    public Task ClickAsync(string elementId, CancellationToken cancellationToken = default) =>
        DispatchAsync("click", elementId, cancellationToken: cancellationToken);

    public Task FillAsync(string elementId, string value, CancellationToken cancellationToken = default) =>
        DispatchAsync("fill", elementId, value, cancellationToken);

    public Task SelectAsync(string elementId, string value, CancellationToken cancellationToken = default) =>
        DispatchAsync("select", elementId, value, cancellationToken);

    public Task CheckAsync(string elementId, CancellationToken cancellationToken = default) =>
        DispatchAsync("check", elementId, cancellationToken: cancellationToken);

    public Task UploadFileAsync(string elementId, string filePath, CancellationToken cancellationToken = default) =>
        DispatchAsync("upload_file", elementId, filePath, cancellationToken);

    public Task ScrollAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("scroll", cancellationToken: cancellationToken);

    private Task DispatchAsync(string type, string elementId = "", string value = "", CancellationToken cancellationToken = default) =>
        _transport.DispatchAsync(new BrowserExtensionCommand(type, elementId, value), cancellationToken);
}
