using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class ResourceIsolationTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private const string LongJobText = "Vaga para pessoa desenvolvedora .NET pleno com React e SQL Server. Empresa Exemplo Tech. Trabalho remoto em tempo integral com beneficios e plano de carreira.";

    [Fact]
    public async Task Automation_ReturnsGone_ForAnonymous()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/automation/discover", new { });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task JobTarget_OfAnotherUser_ReturnsNotFound()
    {
        var (clientA, _) = await CreateLoggedClientAsync();
        var (clientB, _) = await CreateLoggedClientAsync();

        var organize = await clientA.PostAsJsonAsync("/api/job-targets/organize", new { sourceKind = "manual_text", text = LongJobText, useAi = false });
        Assert.Equal(HttpStatusCode.OK, organize.StatusCode);
        var target = await organize.Content.ReadFromJsonAsync<JobTargetDetail>();
        Assert.NotNull(target);

        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/job-targets/{target.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/job-targets/{target.Id}")).StatusCode);
    }

    [Fact]
    public async Task ResumeVersion_OfAnotherUser_ReturnsNotFound()
    {
        var (clientA, userA) = await CreateLoggedClientAsync();
        var (clientB, _) = await CreateLoggedClientAsync();

        Guid versionId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            var docId = Guid.NewGuid();
            db.ResumeDocuments.Add(new ResumeDocument { Id = docId, UserId = userA, Title = "Base" });
            db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userA, ResumeDocumentId = docId, Title = "v1", PlainText = "Texto base para teste de isolamento." });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/resume/versions/{versionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/resume/versions/{versionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/resume/versions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Download_IsNeutral_NotFound_ForOtherUser_AndMissing()
    {
        var (clientA, userA) = await CreateLoggedClientAsync();
        var (clientB, _) = await CreateLoggedClientAsync();

        var tmp = Path.Combine(Path.GetTempPath(), $"iso-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tmp, "curriculo isolado");
        var exportId = Guid.NewGuid();
        try
        {
            await using (var scope = fixture.Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
                db.ResumeExports.Add(new ResumeExport
                {
                    Id = exportId,
                    UserId = userA,
                    ResumeVersionId = Guid.NewGuid(),
                    Format = "txt",
                    FilePath = tmp,
                    ParseText = "curriculo isolado",
                });
                await db.SaveChangesAsync();
            }

            Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/resume/exports/{exportId}/download")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/resume/exports/{exportId}/download")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/resume/exports/{Guid.NewGuid()}/download")).StatusCode);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact]
    public async Task Sections_OfAnotherUser_ReturnsNotFound()
    {
        var (clientA, userA) = await CreateLoggedClientAsync();
        var (clientB, _) = await CreateLoggedClientAsync();

        var versionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userA, Title = "Base" });
            db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userA, ResumeDocumentId = documentId, Title = "v1", PlainText = "Base com seções." });
            db.ResumeSections.Add(new ResumeSection { Id = Guid.NewGuid(), UserId = userA, ResumeVersionId = versionId, SectionType = "summary", Title = "Resumo", SortOrder = 10, ContentJson = "{}" });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/resume/versions/{versionId}/sections")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/resume/versions/{versionId}/sections")).StatusCode);
    }

    [Fact]
    public async Task EvidenceMap_OfAnotherUser_ReturnsNotFound()
    {
        var (clientA, userA) = await CreateLoggedClientAsync();
        var (clientB, _) = await CreateLoggedClientAsync();

        var runId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.ResumeEvidenceMapRuns.Add(new ResumeEvidenceMapRun { Id = runId, UserId = userA, ResumeVersionId = Guid.NewGuid(), JobTargetId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/resume/evidence-maps/{runId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/resume/evidence-maps/{runId}")).StatusCode);
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateLoggedClientAsync()
    {
        var email = $"iso-{Guid.NewGuid():N}@jobpilot.test";
        const string password = "isolation-test-password-123";
        Guid userId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.Users.Add(new AppUser { Id = userId, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) });
            await db.SaveChangesAsync();
        }

        var client = fixture.Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, userId);
    }
}
