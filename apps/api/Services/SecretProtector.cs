using System.Security.Cryptography;
using System.Text;
using JobPilot.Api.Configuration;

namespace JobPilot.Api.Services;

public static class SecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Protect(string? plainText, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return "";
        return ProtectWithKey(plainText, SecurityConfiguration.DataProtectionKey(configuration));
    }

    public static string Unprotect(string? protectedText, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return "";
        return UnprotectWithKey(protectedText, SecurityConfiguration.DataProtectionKey(configuration));
    }

    /// <summary>
    /// Tenta decriptar com a chave atual e, somente durante uma migração explícita,
    /// com a chave legada (ex.: Auth:JwtKey). Retorna true se alguma chave funcionou.
    /// </summary>
    public static bool TryUnprotect(string? protectedText, IConfiguration configuration, out string plainText, out string? matchedKeyLabel)
    {
        plainText = "";
        matchedKeyLabel = null;
        if (string.IsNullOrWhiteSpace(protectedText)) return true;

        foreach (var candidate in KeyCandidates(configuration))
        {
            try
            {
                plainText = UnprotectWithKey(protectedText, candidate.Secret);
                matchedKeyLabel = candidate.Label;
                return true;
            }
            catch (CryptographicException)
            {
                // tenta próxima chave
            }
        }

        return false;
    }

    private static string ProtectWithKey(string plainText, string secretKeyMaterial)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(DeriveKey(secretKeyMaterial), TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    private static string UnprotectWithKey(string protectedText, string secretKeyMaterial)
    {
        var payload = Convert.FromBase64String(protectedText);
        if (payload.Length < NonceSize + TagSize) return "";

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var cipher = payload[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(DeriveKey(secretKeyMaterial), TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static IEnumerable<(string Label, string Secret)> KeyCandidates(IConfiguration configuration)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var configuredPrimary = new[]
        {
            configuration["Security:SecretKey"],
            Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY"),
            Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY", EnvironmentVariableTarget.User)
        }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(configuredPrimary) && seen.Add(configuredPrimary))
            yield return ("Security:SecretKey", configuredPrimary);

        var jwtKey = configuration["Auth:JwtKey"] ?? Environment.GetEnvironmentVariable("Auth__JwtKey");
        if (SecurityConfiguration.AllowsLegacyJwtDataProtection(configuration) &&
            !string.IsNullOrWhiteSpace(jwtKey) && seen.Add(jwtKey))
            yield return ("Auth:JwtKey", jwtKey);
    }

    private static byte[] DeriveKey(string secretKeyMaterial) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(secretKeyMaterial));
}
