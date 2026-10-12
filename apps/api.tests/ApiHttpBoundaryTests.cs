using System.Net;
using System.Net.Http.Json;

namespace JobPilot.Api.Tests;

public sealed class ApiHttpBoundaryTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task Health_IsPublicAndReturnsOk()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":", content);
    }

    [Fact]
    public async Task HealthReady_IsPublicAndReturnsOk()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_IsAvailableOutsideProductionAndDocumentsControllers()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/api/profile\"", content);
        Assert.Contains("\"/api/job-targets", content);
        Assert.Contains("\"/api/resume", content);
    }

    [Fact]
    public async Task Profile_RequiresAuthentication()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentialsReturnUnauthorized()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.com", password = "invalid" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AgentProtocolRoute_RequiresAuthentication()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/agent/profile/validate");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/agent/policy")]
    [InlineData("/api/agent/history")]
    [InlineData("/api/answers")]
    [InlineData("/api/question-patterns")]
    [InlineData("/api/platform-credentials")]
    [InlineData("/api/agent/platform-credentials")]
    [InlineData("/api/profile")]
    [InlineData("/api/profile/validate")]
    [InlineData("/api/profile-validation-rules")]
    [InlineData("/api/resume/ocr/status")]
    public async Task ProtectedGetRoutes_AreRegisteredAndRequireAuthentication(string route)
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/agent/pair")]
    [InlineData("/api/ai/answer-draft")]
    [InlineData("/api/question-patterns/resolve")]
    public async Task ProtectedPostRoutes_AreRegisteredAndRequireAuthentication(string route)
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(route, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExtensionHeartbeat_ReturnsNotFoundAfterExtensionRemoval()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/extension/heartbeat", new
        {
            token = "invalid",
            currentUrl = "",
            isSupportedPage = false,
            browser = "chrome",
            extensionVersion = "test",
            lastError = ""
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRoute_ReturnsNotFound()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/non-existent-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
