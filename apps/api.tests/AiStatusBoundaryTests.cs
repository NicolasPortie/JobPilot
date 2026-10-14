using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobPilot.Api.Tests;

public sealed class AiStatusBoundaryTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private readonly WebApplicationFactory<Program> _factory = fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Kimi:ApiKey", "boundary-test-key");
        builder.ConfigureServices(services => services.AddHttpClient<AiCompletionService>()
            .ConfigurePrimaryHttpMessageHandler(() => new RejectedHandler()));
    });

    [Fact]
    public async Task Status_RequiresAuthentication_AndIsolatesLastResultWithoutSecrets()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/ai/status")).StatusCode);
        var (clientA, userA) = await LoginAsync();
        var (clientB, _) = await LoginAsync();
        using (clientA)
        using (clientB)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var completion = scope.ServiceProvider.GetRequiredService<AiCompletionService>();
            await completion.CompleteTextAsync(userA, "test", "system", "prompt");
            var responseA = await clientA.GetAsync("/api/ai/status");
            var responseB = await clientB.GetAsync("/api/ai/status");
            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
            var own = await responseA.Content.ReadFromJsonAsync<AiStatusResponse>();
            Assert.False(own!.Available);
            Assert.False(own.LastCallSucceeded);
            Assert.Contains("HTTP 403", own.Message);
            var status = await responseB.Content.ReadFromJsonAsync<AiStatusResponse>();
            Assert.NotNull(status);
            Assert.Null(status.LastCallSucceeded);
            Assert.True(status.Available);
            Assert.Null(status.LastCheckedAt);
            var json = await responseA.Content.ReadAsStringAsync();
            Assert.DoesNotContain("boundary-test-key", json);
            Assert.DoesNotContain("echoed-secret", json);
            Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("encrypted", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("baseUrl", json, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(status.Provider));
            Assert.False(string.IsNullOrWhiteSpace(status.Model));
        }
    }

    private sealed class RejectedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("echoed-secret") });
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var id = Guid.NewGuid();
        var email = $"ai-status-{id:N}@jobpilot.test";
        const string password = "ai-status-test-password-123";
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
        db.Users.Add(new AppUser { Id = id, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) });
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (client, id);
    }
}
