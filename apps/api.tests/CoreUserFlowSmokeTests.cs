using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class CoreUserFlowSmokeTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task Login_Profile_And_Resume_CoreFlow_IsReachable()
    {
        var email = $"smoke-{Guid.NewGuid():N}@jobpilot.test";
        const string password = "smoke-test-password";
        using var client = fixture.Factory.CreateClient();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        await AssertOkAsync(await client.GetAsync("/api/profile"));
        await AssertOkAsync(await client.GetAsync("/api/resume/documents"));
        await AssertOkAsync(await client.GetAsync("/api/resume/layouts"));
    }

    private static async Task AssertOkAsync(HttpResponseMessage response) =>
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
}
