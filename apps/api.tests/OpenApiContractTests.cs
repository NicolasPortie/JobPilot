using System.Net;
using System.Text.Json;

namespace JobPilot.Api.Tests;

public sealed class OpenApiContractTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task OpenApi_ContainsOnlyManualContracts()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Contains(paths, p => p.StartsWith("/api/profile"));
        Assert.Contains(paths, p => p.StartsWith("/api/job-targets"));
        Assert.Contains(paths, p => p.StartsWith("/api/resume"));
        Assert.Contains(paths, p => p.StartsWith("/api/auth"));

        string[] legacy =
        [
            "/api/agent", "/api/ai/", "/api/answers", "/api/application-jobs",
            "/api/automation", "/api/checkpoints", "/api/events", "/api/health/operational",
            "/api/platform-credentials", "/api/question-patterns", "/api/runs", "/api/system",
        ];
        foreach (var prefix in legacy)
            Assert.DoesNotContain(paths, p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
