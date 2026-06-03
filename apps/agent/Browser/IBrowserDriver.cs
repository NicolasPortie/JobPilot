using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Browser;

public interface IObservationDriver
{
    string CurrentUrl { get; }

    Task<PageObservation> ObserveAsync();
    Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default);
}

public interface IBrowserDriver : IObservationDriver
{
    Task ClickAsync(string elementId, CancellationToken cancellationToken = default);
    Task FillAsync(string elementId, string value, CancellationToken cancellationToken = default);
    Task SelectAsync(string elementId, string value, CancellationToken cancellationToken = default);
    Task CheckAsync(string elementId, CancellationToken cancellationToken = default);
    Task UploadFileAsync(string elementId, string filePath, CancellationToken cancellationToken = default);
    Task ScrollAsync(CancellationToken cancellationToken = default);
}
