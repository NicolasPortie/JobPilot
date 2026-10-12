using JobPilot.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace JobPilot.Api.Tests;

public class TestesSecurityConfiguration
{
    [Fact]
    public void ValidateRejectsMissingSecrets()
    {
        var configuration = Configuration();

        var error = Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.Validate(configuration, new TestEnvironment("Production")));

        Assert.Contains("Auth:JwtKey", error.Message);
    }

    [Fact]
    public void ValidateRejectsDevelopmentSeedPasswordOutsideDevelopment()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Auth:JwtKey"] = "production-jwt-key-with-more-than-32-characters",
            ["Security:SecretKey"] = "production-data-key-with-more-than-32-characters",
            ["ConnectionStrings:DefaultConnection"] = "Host=database;Database=jobpilot",
            ["Auth:SeedPassword"] = "admin123"
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.Validate(configuration, new TestEnvironment("Production")));

        Assert.Contains("development default", error.Message);
    }

    [Fact]
    public void ValidateAcceptsExplicitProductionSecrets()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Auth:JwtKey"] = "production-jwt-key-with-more-than-32-characters",
            ["Security:SecretKey"] = "production-data-key-with-more-than-32-characters",
            ["ConnectionStrings:DefaultConnection"] = "Host=database;Database=jobpilot"
        });

        SecurityConfiguration.Validate(configuration, new TestEnvironment("Production"));
    }

    private static IConfiguration Configuration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "JobPilot.Api.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
