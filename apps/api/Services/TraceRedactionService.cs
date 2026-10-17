using System.Text.RegularExpressions;

namespace JobPilot.Api.Services;

public sealed class TraceRedactionService
{
    private static readonly Regex SecretAssignment = new(
        @"(?i)(""?(password|senha|token|api[_-]?key|secret|cookie|authorization|bearer)""?\s*[:=]\s*)("".*?""|'[^']*'|[^\s,;}]+)",
        RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(@"(?i)bearer\s+[a-z0-9._\-+/=]{12,}", RegexOptions.Compiled);
    private static readonly Regex LongSecret = new(@"(?i)\b(sk-[a-z0-9_\-]{12,}|(?=[a-z0-9_\-]{32,}\b)(?=[a-z0-9_\-]*[a-z])(?=[a-z0-9_\-]*\d)[a-z0-9_\-]{32,})\b", RegexOptions.Compiled);
    private static readonly Regex Cpf = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BrPhone = new(@"(?<!\d)(?!(?:19|20)\d{2}[\s.-]?(?:19|20)\d{2}(?!\d))(?:\+?55[\s.-]?)?(?:\(?\d{2}\)?[\s.-]?)?\d{4,5}[\s.-]?\d{4}(?!\d)", RegexOptions.Compiled);

    public string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var redacted = SecretAssignment.Replace(value, match => $"{match.Groups[1].Value}\"[redacted-secret]\"");
        redacted = BearerToken.Replace(redacted, "Bearer [redacted-secret]");
        redacted = LongSecret.Replace(redacted, "[redacted-secret]");
        redacted = Cpf.Replace(redacted, "[redacted-cpf]");
        return redacted;
    }

    public string RedactPii(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var redacted = Redact(value);
        redacted = Email.Replace(redacted, "[redacted-email]");
        redacted = BrPhone.Replace(redacted, "[redacted-phone]");
        return redacted;
    }

    /// <summary>
    /// Troca e-mail e telefone por marcadores numerados e guarda o original em <paramref name="restore"/>,
    /// para que o texto devolvido pela IA recupere os contatos sem que eles saiam da máquina.
    /// </summary>
    public string RedactPii(string value, IDictionary<string, string> restore)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var redacted = Redact(value);
        redacted = Email.Replace(redacted, match => Placeholder("email", match.Value, restore));
        return BrPhone.Replace(redacted, match => Placeholder("phone", match.Value, restore));
    }

    public string? Restore(string? value, IReadOnlyDictionary<string, string> restore)
    {
        if (string.IsNullOrEmpty(value) || restore.Count == 0) return value;
        foreach (var (placeholder, original) in restore)
            value = value.Replace(placeholder, original, StringComparison.OrdinalIgnoreCase);
        return value;
    }

    private static string Placeholder(string kind, string original, IDictionary<string, string> restore)
    {
        var existing = restore.FirstOrDefault(pair => pair.Value == original);
        if (existing.Key is not null) return existing.Key;
        var placeholder = $"[redacted-{kind}-{restore.Count + 1}]";
        restore[placeholder] = original;
        return placeholder;
    }
}
