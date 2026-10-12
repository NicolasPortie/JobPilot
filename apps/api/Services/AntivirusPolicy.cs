namespace JobPilot.Api.Services;

public static class AntivirusPolicy
{
    public static string? WarningFor(IConfiguration configuration)
    {
        var mode = (configuration["Antivirus:Mode"] ?? "none").Trim().ToLowerInvariant();
        return mode is "none" or ""
            ? "Antivírus não configurado neste ambiente; o arquivo fica restrito ao seu usuário e não passa por varredura de malware."
            : null;
    }
}
