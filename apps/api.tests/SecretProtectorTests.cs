using JobPilot.Api.Services;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class SecretProtectorTests
{
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
