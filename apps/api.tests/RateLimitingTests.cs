using System.Net;
using System.Net.Http.Headers;
using JobPilot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task AnalysisQuota_IsIndependentForAuthenticatedUsers()
    {
        using var factory = new ApiTestFactory();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateToken(Guid.NewGuid(), "first@example.test", "user"));
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.CreateToken(Guid.NewGuid(), "second@example.test", "user"));

        for (var index = 0; index < 20; index++)
        {
            using var response = await first.GetAsync("/api/resume/layouts");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var otherUser = await second.GetAsync("/api/resume/layouts", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, otherUser.StatusCode);
    }
}
