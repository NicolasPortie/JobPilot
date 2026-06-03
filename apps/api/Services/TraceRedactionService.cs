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

    public string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var redacted = SecretAssignment.Replace(value, match => $"{match.Groups[1].Value}\"[redacted-secret]\"");
        redacted = BearerToken.Replace(redacted, "Bearer [redacted-secret]");
        redacted = LongSecret.Replace(redacted, "[redacted-secret]");
        redacted = Cpf.Replace(redacted, "[redacted-cpf]");
        return redacted;
    }
}
