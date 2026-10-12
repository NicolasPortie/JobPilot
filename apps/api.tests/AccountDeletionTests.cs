using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class AccountDeletionTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private static object ProfilePayload(string fullName, string email) => new
    {
        fullName,
        email,
        phone = "",
        city = "",
        state = "",
        country = "Brasil",
        linkedinUrl = "",
        githubUrl = "",
        portfolioUrl = "",
        professionalSummary = "",
        skills = Array.Empty<string>(),
        targetRole = "",
        seniority = "unknown",
        salaryExpectation = ""
    };

    [Fact]
    public async Task DeleteProfile_RemovesProfile_KeepsLogin()
    {
        var (client, _) = await CreateLoggedClientAsync();
        var save = await client.PutAsJsonAsync("/api/profile", ProfilePayload("Base", "a@x.com"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var del = await client.DeleteAsync("/api/profile");
        Assert.True(del.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, await del.Content.ReadAsStringAsync());

        var get = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var after = await get.Content.ReadFromJsonAsync<CandidateProfileResponse>();
        Assert.NotNull(after);
        Assert.Null(after.Id);
    }

    [Fact]
    public async Task DeleteAccount_RemovesUserAndData_SecondUserUnaffected()
    {
        var (clientA, emailA) = await CreateLoggedClientAsync();
        var (clientB, emailB) = await CreateLoggedClientAsync();
        Assert.NotEqual(emailA, emailB);

        await clientA.PutAsJsonAsync("/api/profile", ProfilePayload("A", emailA));
        await clientB.PutAsJsonAsync("/api/profile", ProfilePayload("B", emailB));

        var organize = await clientA.PostAsJsonAsync("/api/job-targets/organize", new { sourceKind = "manual_text", text = "Vaga para pessoa desenvolvedora .NET pleno com React e SQL Server. Empresa Exemplo Tech. Trabalho remoto em tempo integral com beneficios e plano de carreira.", useAi = false });
        Assert.Equal(HttpStatusCode.OK, organize.StatusCode);

        Guid userAId, userBId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            userAId = db.Users.First(u => u.Email == emailA).Id;
            userBId = db.Users.First(u => u.Email == emailB).Id;
            Assert.NotEmpty(db.JobTargets.Where(t => t.UserId == userAId).ToList());
            Assert.NotEmpty(db.RefreshTokens.Where(t => t.UserId == userAId).ToList());
        }

        var del = await clientA.DeleteAsync("/api/auth/account");
        Assert.True(del.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, await del.Content.ReadAsStringAsync());

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            Assert.Null(db.Users.FirstOrDefault(u => u.Email == emailA));
            Assert.NotNull(db.Users.FirstOrDefault(u => u.Email == emailB));
            Assert.Empty(db.CandidateProfiles.Where(p => p.UserId == userAId).ToList());
            Assert.Empty(db.JobTargets.Where(t => t.UserId == userAId).ToList());
            Assert.Empty(db.JobRequirements.Where(r => r.UserId == userAId).ToList());
            Assert.Empty(db.RefreshTokens.Where(t => t.UserId == userAId).ToList());
            Assert.NotEmpty(db.RefreshTokens.Where(t => t.UserId == userBId).ToList());
            Assert.NotEmpty(db.CandidateProfiles.Where(p => p.UserId == userBId).ToList());
        }

        var stillOk = await clientB.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, stillOk.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RequiresAuth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.DeleteAsync("/api/auth/account");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetentionPolicy_IsVisible()
    {
        var (client, _) = await CreateLoggedClientAsync();
        var response = await client.GetAsync("/api/profile/retention");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("resumeOriginals", body);
    }

    [Fact]
    public async Task RetentionCleanup_RemovesExpiredLogsAndImports()
    {
        Guid userId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            userId = Guid.NewGuid();
            db.Users.Add(new AppUser { Id = userId, Email = $"old-{Guid.NewGuid():N}@jobpilot.test", PasswordHash = "x" });
            db.AiUsageLogs.Add(new AiUsageLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Operation = "test",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-200),
            });
            db.ResumeImports.Add(new ResumeImport
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SourceFileName = "old.pdf",
                SourcePath = "",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-200),
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<RetentionService>();
            var result = await retention.CleanupExpiredAsync(DateTimeOffset.UtcNow);
            Assert.Equal(1, result.AiUsageLogsRemoved);

            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            Assert.Empty(db.AiUsageLogs.Where(x => x.UserId == userId).ToList());
            Assert.Empty(db.ResumeImports.Where(x => x.UserId == userId).ToList());
        }
    }

    private async Task<(HttpClient Client, string Email)> CreateLoggedClientAsync()
    {
        var email = $"del-{Guid.NewGuid():N}@jobpilot.test";
        const string password = "delete-test-password-123";
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

        var client = fixture.Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, email);
    }
}
