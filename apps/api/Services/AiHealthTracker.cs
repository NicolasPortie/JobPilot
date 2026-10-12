namespace JobPilot.Api.Services;

/// <summary>
/// Guarda o resultado da última chamada de IA para que a interface consiga dizer
/// se a IA está respondendo, em vez de cair no modo determinístico em silêncio.
/// </summary>
public sealed class AiHealthTracker
{
    private readonly Lock _gate = new();
    private AiHealthSnapshot? _last;

    public void RecordSuccess(string providerName, string model) =>
        Set(new AiHealthSnapshot(providerName, model, true, null, DateTimeOffset.UtcNow));

    public void RecordFailure(string providerName, string model, string error) =>
        Set(new AiHealthSnapshot(providerName, model, false, error, DateTimeOffset.UtcNow));

    public AiHealthSnapshot? Snapshot()
    {
        lock (_gate) return _last;
    }

    private void Set(AiHealthSnapshot snapshot)
    {
        lock (_gate) _last = snapshot;
    }
}

public sealed record AiHealthSnapshot(string ProviderName, string Model, bool LastSucceeded, string? LastError, DateTimeOffset CheckedAt);
