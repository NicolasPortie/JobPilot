using System.Net;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public static class AiProviderEndpointPolicy
{
    public static void Validate(AiProvider provider, IConfiguration configuration) =>
        Validate(provider.ProviderType, provider.BaseUrl, configuration);

    private static void Validate(string providerType, string baseUrl, IConfiguration configuration)
    {
        if (!Uri.TryCreate((baseUrl ?? "").Trim(), UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrWhiteSpace(uri.UserInfo) ||
            !string.IsNullOrWhiteSpace(uri.Query) ||
            !string.IsNullOrWhiteSpace(uri.Fragment) ||
            (!uri.IsDefaultPort && uri.Port is not (80 or 443 or 11434)))
            throw new InvalidOperationException("O endpoint de IA informado não é válido.");

        var type = (providerType ?? "").Trim().ToLowerInvariant();
        if (type == "ollama")
        {
            if (uri.Scheme is not ("http" or "https") ||
                !IsLoopbackHost(uri.Host) ||
                uri.Port is not (80 or 443 or 11434))
                throw new InvalidOperationException("Ollama deve apontar somente para o servico local.");
            return;
        }

        if (uri.Scheme != "https" || uri.Port == 11434)
            throw new InvalidOperationException("Provedores externos de IA precisam usar HTTPS.");

        var allowedHosts = (configuration["Ai:AllowedHosts"] ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var knownHost = type switch
        {
            "kimi" => OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, "api.kimi.com"),
            "openai" => OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, "api.openai.com"),
            "anthropic" => OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, "api.anthropic.com"),
            "openai-compatible" => OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, "api.openai.com"),
            _ => false
        };
        var explicitlyAllowed = allowedHosts.Any(host => OutboundUrlPolicy.IsHostOrSubdomain(uri.Host, host));
        if (!knownHost && !explicitlyAllowed)
            throw new InvalidOperationException("Este host de IA não está na lista de destinos confiáveis.");

        if (IPAddress.TryParse(uri.Host, out var address) && !OutboundUrlPolicy.IsPublicAddressForPolicy(address))
            throw new InvalidOperationException("O endpoint de IA não pode apontar para uma rede privada.");
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
}
