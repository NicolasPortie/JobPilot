using System.Net;
using System.Net.Sockets;

namespace JobPilot.Api.Services;

/// <summary>
/// Keeps user-provided public links away from local, private and metadata
/// networks before the application makes an outbound request.
/// </summary>
public static class OutboundUrlPolicy
{
    private static readonly string[] BlockedHostSuffixes =
    [
        ".localhost",
        ".local",
        ".internal",
        ".home.arpa",
        ".test",
        ".invalid"
    ];

    public static bool TryValidatePublicHttpUrl(string value, out Uri uri)
    {
        uri = default!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            candidate.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(candidate.Host) ||
            !string.IsNullOrWhiteSpace(candidate.UserInfo) ||
            (!candidate.IsDefaultPort && candidate.Port is not (80 or 443)))
            return false;

        var host = candidate.DnsSafeHost.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host) || IsBlockedHost(host))
            return false;

        if (IPAddress.TryParse(host, out var address) && !IsPublicAddress(address))
            return false;

        uri = candidate;
        return true;
    }

    public static void EnsurePublicHttpUrl(string value)
    {
        if (!TryValidatePublicHttpUrl(value, out _))
            throw new InvalidOperationException("A URL precisa ser HTTP(S), pública e sem credenciais embutidas.");
    }

    public static bool IsHostOrSubdomain(string host, string domain)
    {
        var normalizedHost = (host ?? "").TrimEnd('.').Trim();
        var normalizedDomain = (domain ?? "").TrimEnd('.').Trim();
        return normalizedHost.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
               normalizedHost.EndsWith($".{normalizedDomain}", StringComparison.OrdinalIgnoreCase);
    }

    public static HttpMessageHandler CreateHandler(bool allowLocalOllama = false)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(8)
        };
        handler.ConnectCallback = (context, cancellationToken) =>
            ConnectToPublicAddressAsync(context, cancellationToken, allowLocalOllama);
        return handler;
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken,
        bool allowLocalOllama)
    {
        var host = context.DnsEndPoint.Host.TrimEnd('.');
        var isLocalOllama = allowLocalOllama &&
            context.DnsEndPoint.Port == 11434 &&
            IsLoopbackHost(host);
        if (IsBlockedHost(host) && !isLocalOllama)
            throw new HttpRequestException("A conexão para este host foi bloqueada pela política de rede.");

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        foreach (var address in addresses.Where(address => isLocalOllama
            ? IPAddress.IsLoopback(address)
            : IsPublicAddress(address)))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
            }
        }

        throw new HttpRequestException("O host nao resolveu para um endereco publico permitido.");
    }

    private static bool IsBlockedHost(string host)
    {
        var normalized = host.TrimEnd('.').ToLowerInvariant();
        return normalized is "localhost" or "metadata.google.internal" ||
               !normalized.Contains('.') ||
               BlockedHostSuffixes.Any(normalized.EndsWith);
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    public static bool IsPublicAddressForPolicy(IPAddress address) => IsPublicAddress(address);

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) ||
            IPAddress.Any.Equals(address) ||
            IPAddress.IPv6Any.Equals(address) ||
            IPAddress.None.Equals(address) ||
            IPAddress.IPv6None.Equals(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast)
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];
            var isPrivateOrReserved =
                first == 0 ||
                first == 10 ||
                first == 127 ||
                first == 169 && second == 254 ||
                first == 172 && second >= 16 && second <= 31 ||
                first == 192 && second == 168 ||
                first == 192 && second == 0 ||
                first == 192 && second == 2 ||
                first == 198 && second >= 18 && second <= 19 ||
                first == 198 && second == 51 ||
                first == 203 && second == 0 ||
                first == 100 && second >= 64 && second <= 127 ||
                first >= 224;
            return !isPrivateOrReserved;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var first = address.GetAddressBytes()[0];
            return (first & 0xFE) != 0xFC;
        }

        return false;
    }
}
