using System.Text.Json;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Infrastructure;

namespace JobPilot.Agent.Browser;

public sealed class BrowserExtensionHttpTransport : IBrowserExtensionTransport
{
    private const string RequiredExtensionVersion = "0.1.4";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly JobPilotApiClient _client;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;

    public BrowserExtensionHttpTransport(JobPilotApiClient client, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        _client = client;
        _timeout = timeout ?? TimeSpan.FromSeconds(45);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public async Task<PageObservation> ObserveAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendAsync("observe", "", "", "observe", cancellationToken);
        return JsonSerializer.Deserialize<PageObservation>(result, JsonOptions)
               ?? throw new InvalidOperationException("Extension returned an empty observation.");
    }

    public async Task<string> ScreenshotAsync(string reasonCode, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync("screenshot", "", "", reasonCode, cancellationToken);
        using var document = JsonDocument.Parse(result);
        if (document.RootElement.TryGetProperty("dataUrl", out var dataUrl))
            return dataUrl.GetString() ?? "";

        return result;
    }

    public async Task DispatchAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(command, cancellationToken);
        using var document = JsonDocument.Parse(result);
        if (document.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var error = document.RootElement.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : "extension_command_failed";
            throw new InvalidOperationException(error);
        }
    }

    public Task<string> ExecuteAsync(BrowserExtensionCommand command, CancellationToken cancellationToken = default) =>
        SendAsync(command.Type, command.ElementId, command.Value, command.ReasonCode, cancellationToken);

    private async Task<string> SendAsync(string type, string elementId, string value, string reasonCode, CancellationToken cancellationToken)
    {
        var session = await _client.GetExtensionStatusAsync();
        if (session?.SessionId is null || session.Status != "connected")
            throw new InvalidOperationException("Browser extension is not connected.");
        if (!IsVersionAtLeast(session.ExtensionVersion, RequiredExtensionVersion))
            throw new InvalidOperationException($"Browser extension version {DisplayVersion(session.ExtensionVersion)} is outdated. Reload JobPilot extension version {RequiredExtensionVersion} in chrome://extensions.");

        var queued = await _client.QueueExtensionCommandAsync(new QueueExtensionCommandRequest(session.SessionId.Value, type, elementId, value, reasonCode));
        if (queued is null)
            throw new InvalidOperationException("Could not queue browser extension command.");

        var deadline = DateTimeOffset.UtcNow + _timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await _client.GetExtensionCommandAsync(queued.Id);
            if (status is null)
                throw new InvalidOperationException("Queued browser extension command disappeared.");

            if (status.Status == "completed")
                return string.IsNullOrWhiteSpace(status.ResultJson) ? "{}" : status.ResultJson;

            if (status.Status == "failed")
            {
                if (!string.IsNullOrWhiteSpace(status.ResultJson))
                    return status.ResultJson;

                throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.Error) ? "Browser extension command failed." : status.Error);
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for browser extension command '{type}'.");
    }

    private static bool IsVersionAtLeast(string value, string minimum)
    {
        var currentParts = ParseVersion(value);
        var minimumParts = ParseVersion(minimum);
        for (var i = 0; i < Math.Max(currentParts.Length, minimumParts.Length); i++)
        {
            var current = i < currentParts.Length ? currentParts[i] : 0;
            var required = i < minimumParts.Length ? minimumParts[i] : 0;
            if (current > required) return true;
            if (current < required) return false;
        }

        return true;
    }

    private static int[] ParseVersion(string value) =>
        (value ?? "")
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var number) ? number : 0)
            .ToArray();

    private static string DisplayVersion(string version) =>
        string.IsNullOrWhiteSpace(version) ? "unknown" : version;
}
