using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class AiSettingsTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private const string Secret = "sk-ui-secret-1234567890";

    private readonly WebApplicationFactory<Program> _factory = fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Ai:AllowedHosts", "gateway.example.com");
        builder.ConfigureServices(services => services.AddHttpClient<AiCompletionService>()
            .ConfigurePrimaryHttpMessageHandler(() => new OkAnthropicHandler()));
    });

    private static AiSettingsRequest Gateway(string? key = Secret, string host = "gateway.example.com", bool enabled = true) =>
        new("openai-compatible", "OmniRoute", $"https://{host}/v1", "antigravity/gemini-3.8-flash-combo", key, enabled);

    [Fact]
    public async Task Settings_RequireAuthentication()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/ai/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/ai/settings", Gateway())).StatusCode);
    }

    [Fact]
    public async Task Save_StoresEncryptedKey_AndNeverReturnsIt()
    {
        var (client, userId) = await LoginAsync();
        using var _ = client;

        var empty = await client.GetFromJsonAsync<AiSettingsResponse>("/api/ai/settings");
        Assert.False(empty!.Saved);
        Assert.Contains(empty.Presets, x => x.ProviderType == "openai-compatible");

        var response = await client.PutAsJsonAsync("/api/ai/settings", Gateway());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, json);
        var saved = await client.GetFromJsonAsync<AiSettingsResponse>("/api/ai/settings");
        Assert.True(saved!.Saved);
        Assert.True(saved.HasApiKey);
        Assert.Equal("saved", saved.ActiveSource);
        Assert.Equal("OmniRoute", saved.ActiveProvider);
        Assert.Equal("https://gateway.example.com/v1", saved.BaseUrl);

        var row = await ReadRowAsync(userId);
        Assert.DoesNotContain(Secret, row.EncryptedApiKey);
        Assert.Equal(Secret, DecryptKey(row));
    }

    [Fact]
    public async Task Save_WithBlankKey_KeepsTheSavedKey_WhenTheDestinationDidNotChange()
    {
        var (client, userId) = await LoginAsync();
        using var _ = client;
        await client.PutAsJsonAsync("/api/ai/settings", Gateway());

        var renamed = Gateway(key: "") with { Model = "outro-modelo" };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/ai/settings", renamed)).StatusCode);

        var row = await ReadRowAsync(userId);
        Assert.Equal("outro-modelo", row.Model);
        Assert.Equal(Secret, DecryptKey(row));
    }

    [Fact]
    public async Task Save_WithBlankKey_DoesNotReuseTheKeyForAnotherProvider()
    {
        var (client, userId) = await LoginAsync();
        using var _ = client;
        await client.PutAsJsonAsync("/api/ai/settings", Gateway());

        var other = new AiSettingsRequest("anthropic", "Claude", "https://api.anthropic.com", "claude-haiku-5-5", "", true);
        var response = await client.PutAsJsonAsync("/api/ai/settings", other);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Secret, DecryptKey(await ReadRowAsync(userId)));
    }

    [Fact]
    public async Task Save_RejectsUnknownProvider_UntrustedHostAndPlainHttp()
    {
        var (client, _) = await LoginAsync();
        using var _c = client;

        var unknown = Gateway() with { ProviderType = "mystery" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/settings", unknown)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/settings", Gateway(host: "evil.example.net"))).StatusCode);
        var http = Gateway() with { BaseUrl = "http://gateway.example.com/v1" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/ai/settings", http)).StatusCode);
    }

    [Fact]
    public async Task Settings_AreIsolatedPerUser_AndCanBeRemoved()
    {
        var (clientA, _) = await LoginAsync();
        var (clientB, _) = await LoginAsync();
        using var _a = clientA;
        using var _b = clientB;
        await clientA.PutAsJsonAsync("/api/ai/settings", Gateway());

        Assert.False((await clientB.GetFromJsonAsync<AiSettingsResponse>("/api/ai/settings"))!.Saved);
        Assert.Equal(HttpStatusCode.NoContent, (await clientA.DeleteAsync("/api/ai/settings")).StatusCode);
        Assert.False((await clientA.GetFromJsonAsync<AiSettingsResponse>("/api/ai/settings"))!.Saved);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync("/api/ai/settings")).StatusCode);
    }

    [Fact]
    public async Task Test_ReportsSuccess_AndExplainsBlockedDestinations_WithoutSaving()
    {
        var (client, userId) = await LoginAsync();
        using var _ = client;

        var ok = await (await client.PostAsJsonAsync("/api/ai/settings/test", Gateway())).Content.ReadFromJsonAsync<AiProviderTestResponse>();
        Assert.True(ok!.Ok);

        var blocked = await (await client.PostAsJsonAsync("/api/ai/settings/test", Gateway(host: "evil.example.net"))).Content.ReadFromJsonAsync<AiProviderTestResponse>();
        Assert.False(blocked!.Ok);
        Assert.Contains("confiáveis", blocked.Error);

        var noKey = await (await client.PostAsJsonAsync("/api/ai/settings/test", Gateway(key: ""))).Content.ReadFromJsonAsync<AiProviderTestResponse>();
        Assert.False(noKey!.Ok);
        Assert.Contains("chave", noKey.Error, StringComparison.OrdinalIgnoreCase);

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<JobPilotDbContext>().AiProviders.AnyAsync(x => x.UserId == userId));
    }

    private async Task<AiProvider> ReadRowAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JobPilotDbContext>().AiProviders.AsNoTracking().SingleAsync(x => x.UserId == userId);
    }

    private string DecryptKey(AiProvider row)
    {
        using var scope = _factory.Services.CreateScope();
        return SecretProtector.Unprotect(row.EncryptedApiKey, scope.ServiceProvider.GetRequiredService<IConfiguration>());
    }

    private sealed class OkAnthropicHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}", System.Text.Encoding.UTF8, "application/json")
            });
    }

    private async Task<(HttpClient Client, Guid UserId)> LoginAsync()
    {
        var id = Guid.NewGuid();
        var email = $"ai-settings-{id:N}@jobpilot.test";
        const string password = "ai-settings-test-password-123";
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
