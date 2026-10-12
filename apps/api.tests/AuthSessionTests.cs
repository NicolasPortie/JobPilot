using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class AuthSessionTests
{
    [Fact]
    public async Task Login_ReturnsRefreshToken_AndAccessHasNameIdentifier()
    {
        using var _ = CreateIsolated(out var client, out var email, out var password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(auth.Token);
        Assert.NotNull(jwt.Claims.FirstOrDefault(c => c.Type is "nameid" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));
    }

    [Fact]
    public async Task Refresh_Rotates_AndOldTokenRejected()
    {
        using var _ = CreateIsolated(out var client, out var email, out var password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(renewed);
        Assert.NotEqual(auth.RefreshToken, renewed.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Logout_Revokes_RefreshRejectedAfter()
    {
        using var _ = CreateIsolated(out var client, out var email, out var password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task FiveFailedLogins_LockAccount()
    {
        using var _ = CreateIsolated(out var client, out var email, out var realPassword);
        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = realPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        var body = await locked.Content.ReadAsStringAsync();
        Assert.Contains("bloqueada", body);
    }

    [Fact]
    public async Task Refresh_AfterAbsoluteLifetime_Rejected()
    {
        var factory = CreateIsolated(out var client, out var email, out var password);
        using (factory)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(auth);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
                foreach (var token in db.RefreshTokens.ToList())
                {
                    token.RootCreatedAt = DateTimeOffset.UtcNow.AddDays(-31);
                    token.CreatedAt = DateTimeOffset.UtcNow.AddDays(-31);
                }
                db.SaveChanges();
            }

            var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
            Assert.Contains("tempo máximo", await refresh.Content.ReadAsStringAsync());
        }
    }

    private static ApiTestFactory CreateIsolated(out HttpClient client, out string email, out string password)
    {
        var factory = new ApiTestFactory();
        email = $"sess-{Guid.NewGuid():N}@jobpilot.test";
        password = "session-test-password-123";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.Users.Add(new AppUser { Id = Guid.NewGuid(), Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) });
            db.SaveChanges();
        }
        client = factory.CreateClient();
        return factory;
    }
}
