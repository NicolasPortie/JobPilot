using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Mantém o estado de uma sessão de aplicação em uma única vaga.
/// Evita duplicatas, rastrea campos preenchidos e passos do wizard.
/// </summary>
public sealed class SessionContext
{
    /// <summary>CampoKey → (valor usado, tipo semântico)</summary>
    private readonly Dictionary<string, (string Value, SemanticFieldType Type)> _assignments = new();
    private readonly Queue<string> _decisionMemory = new();

    /// <summary>Tipos que NÃO devem compartilhar o mesmo valor</summary>
    private static readonly Dictionary<SemanticFieldType, HashSet<SemanticFieldType>> UnrelatedTypes = new()
    {
        [SemanticFieldType.Cpf] = [SemanticFieldType.FullName, SemanticFieldType.FirstName, SemanticFieldType.LastName, SemanticFieldType.Email, SemanticFieldType.Phone, SemanticFieldType.Address],
        [SemanticFieldType.Phone] = [SemanticFieldType.FullName, SemanticFieldType.FirstName, SemanticFieldType.LastName, SemanticFieldType.Email, SemanticFieldType.Cpf, SemanticFieldType.Address],
        [SemanticFieldType.Email] = [SemanticFieldType.FullName, SemanticFieldType.FirstName, SemanticFieldType.LastName, SemanticFieldType.Cpf, SemanticFieldType.Phone],
        [SemanticFieldType.FullName] = [SemanticFieldType.Cpf, SemanticFieldType.Phone, SemanticFieldType.Email, SemanticFieldType.Salary],
        [SemanticFieldType.FirstName] = [SemanticFieldType.Cpf, SemanticFieldType.Phone, SemanticFieldType.Email],
        [SemanticFieldType.LastName] = [SemanticFieldType.Cpf, SemanticFieldType.Phone, SemanticFieldType.Email],
        [SemanticFieldType.Salary] = [SemanticFieldType.FullName, SemanticFieldType.FirstName, SemanticFieldType.LastName, SemanticFieldType.Cpf, SemanticFieldType.Phone, SemanticFieldType.Email],
        [SemanticFieldType.BirthDate] = [SemanticFieldType.FullName, SemanticFieldType.FirstName, SemanticFieldType.LastName, SemanticFieldType.Cpf],
    };

    public int CurrentStep { get; set; } = 0;
    public int TotalSteps { get; set; } = 0;
    public int RetryCount { get; set; } = 0;
    public string? LastNavigationActionKey { get; private set; }
    public int RepeatedNavigationActionCount { get; private set; }

    /// <summary>
    /// Verifica se um valor já foi usado em um campo de tipo NÃO relacionado.
    /// Ex: se "Nicolas Portie Suss" já foi usado em FullName,
    /// não deve ser usado em Cpf, Phone, Email, etc.
    /// </summary>
    public bool WasValueUsed(string value, SemanticFieldType forType)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();

        if (!UnrelatedTypes.TryGetValue(forType, out var unrelated))
            return false;

        foreach (var (_, entry) in _assignments)
        {
            if (entry.Value.Trim().Equals(normalized, StringComparison.OrdinalIgnoreCase)
                && unrelated.Contains(entry.Type))
            {
                return true;
            }
        }
        return false;
    }

    public void Record(string fieldKey, string value, SemanticFieldType type)
    {
        _assignments[fieldKey] = (value, type);
    }

    public bool HasField(string fieldKey) => _assignments.ContainsKey(fieldKey);

    public string? GetAssignedValue(string fieldKey) =>
        _assignments.TryGetValue(fieldKey, out var entry) ? entry.Value : null;

    public IReadOnlyDictionary<string, (string Value, SemanticFieldType Type)> AllAssignments => _assignments;

    public int RecordNavigationAction(BrowserAction action)
    {
        var key = $"{action.Type}:{action.ElementId}:{action.Label}";
        if (string.Equals(LastNavigationActionKey, key, StringComparison.Ordinal))
            RepeatedNavigationActionCount++;
        else
        {
            LastNavigationActionKey = key;
            RepeatedNavigationActionCount = 1;
        }

        return RepeatedNavigationActionCount;
    }

    public void ResetNavigationAction()
    {
        LastNavigationActionKey = null;
        RepeatedNavigationActionCount = 0;
    }

    public void RecordDecisionOutcome(BrowserAction action, bool success, string result)
    {
        var entry = $"{action.Type}:{action.Label}:{action.ElementId} => {(success ? "success" : "failed")} ({result})";
        _decisionMemory.Enqueue(entry.Length > 260 ? entry[..260] : entry);
        while (_decisionMemory.Count > 6)
            _decisionMemory.Dequeue();
    }

    public string DecisionMemorySummary() =>
        _decisionMemory.Count == 0 ? "No previous actions in this application loop." : string.Join(" | ", _decisionMemory);
}
