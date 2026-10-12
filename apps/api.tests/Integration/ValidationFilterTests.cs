using System.Net;
using System.Net.Http.Json;

namespace JobPilot.Api.Tests.Integration;

public sealed class ValidationFilterTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task Login_EmptyEmail_ReturnsBadRequestWithValidationErrors()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "", password = "123456" });
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", content);
    }

    [Fact]
    public async Task Login_ShortPassword_ReturnsBadRequestWithValidationErrors()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "test@test.com", password = "123" });
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Password", content);
    }
}
