using System.Security.Cryptography;
using System.Text;

namespace JobPilot.Api.Services;

public static class SecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Protect(string? plainText, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return "";
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(Key(configuration), TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public static string Unprotect(string? protectedText, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return "";
        var payload = Convert.FromBase64String(protectedText);
        if (payload.Length < NonceSize + TagSize) return "";

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var cipher = payload[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(Key(configuration), TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] Key(IConfiguration configuration)
    {
        var secret = configuration["Security:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY")
            ?? Environment.GetEnvironmentVariable("JOBPILOT_SECRET_KEY", EnvironmentVariableTarget.User)
            ?? configuration["Auth:JwtKey"]
            ?? "dev-only-change-this-key-please-32";
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}
