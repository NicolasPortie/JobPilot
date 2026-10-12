using JobPilot.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void ApplicationServices_CanBuildWithValidationEnabled()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters",
            ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters",
            ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused",
        });
        builder.Services.AddJobPilotServices(builder.Configuration);

        using var app = builder.Build();

        Assert.NotNull(app.Services);
    }
}
