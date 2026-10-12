using JobPilot.Api.Services;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void TryUnprotect_WithLegacyJwtKey_DecryptsWhenPrimaryKeyDiffers()
    {
        var jwtConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "legacy-jwt-key-with-more-than-32-characters",
                ["Security:AllowLegacyJwtDataProtection"] = "true"
            })
            .Build();
        var currentConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "legacy-jwt-key-with-more-than-32-characters",
                ["Security:SecretKey"] = "current-secret-key-with-more-than-32-chars",
                ["Security:AllowLegacyJwtDataProtection"] = "true"
            })
            .Build();

        var encrypted = SecretProtector.Protect("linkedin-pass", jwtConfig);

        var ok = SecretProtector.TryUnprotect(encrypted, currentConfig, out var plain, out var label);

        Assert.True(ok);
        Assert.Equal("linkedin-pass", plain);
        Assert.Equal("Auth:JwtKey", label);
    }

    [Fact]
    public void DataProtectionDoesNotSilentlyReuseJwtKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "legacy-jwt-key-with-more-than-32-characters"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => SecretProtector.Protect("secret", configuration));

        Assert.Contains("independent", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProtectAndUnprotectRoundTrip()
    {
        var configuration = Configuration("test-master-key-32-characters");

        var encrypted = SecretProtector.Protect("kimi-secret-key", configuration);
        var decrypted = SecretProtector.Unprotect(encrypted, configuration);

        Assert.NotEqual("kimi-secret-key", encrypted);
        Assert.Equal("kimi-secret-key", decrypted);
    }

    [Fact]
    public void ProtectUsesFreshNonce()
    {
        var configuration = Configuration("test-master-key-32-characters");

        var first = SecretProtector.Protect("same-secret", configuration);
        var second = SecretProtector.Protect("same-secret", configuration);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EmptySecretStaysEmpty()
    {
        var configuration = Configuration("test-master-key-32-characters");

        Assert.Equal("", SecretProtector.Protect("", configuration));
        Assert.Equal("", SecretProtector.Unprotect("", configuration));
    }

    private static IConfiguration Configuration(string key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretKey"] = key
            })
            .Build();
}
