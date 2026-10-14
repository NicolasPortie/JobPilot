namespace JobPilot.Api.Services;

/// <summary>Último resultado por usuário e configuração de provedor, sem credenciais.</summary>
public sealed class AiHealthTracker
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, AiHealthSnapshot> _last = new();

    public void RecordSuccess(Guid userId, string identity) =>
        Set(userId, new(identity, true, null, DateTimeOffset.UtcNow));

    public void RecordFailure(Guid userId, string identity, string error) =>
        Set(userId, new(identity, false, error, DateTimeOffset.UtcNow));

    public AiHealthSnapshot? Snapshot(Guid userId, string identity)
    {
        lock (_gate)
            return _last.TryGetValue(userId, out var value) && value.Identity == identity ? value : null;
    }

    private void Set(Guid userId, AiHealthSnapshot snapshot)
    {
        lock (_gate) _last[userId] = snapshot;
    }
}

public sealed record AiHealthSnapshot(string Identity, bool LastSucceeded, string? LastError, DateTimeOffset CheckedAt);
