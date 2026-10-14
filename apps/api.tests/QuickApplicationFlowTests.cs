using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobPilot.Api.Tests;

public sealed class QuickApplicationFlowTests(ApiTestFixture fixture) : IClassFixture<ApiTestFixture>
{
    private const string Posting = """
        Desenvolvedor Backend .NET
        Empresa: Horizonte Pagamentos
        Local: Remoto

        Requisitos
        - Experiência com C#, .NET e SQL
        - Docker e Git

        Diferenciais
        - Redis
        """;

    private async Task<(HttpClient Client, Guid UserId, Guid BaseVersionId)> SignInWithBaseResumeAsync()
    {
        var email = $"quick-{Guid.NewGuid():N}@jobpilot.test";
        const string password = "quick-test-password";
        var client = fixture.Factory.CreateClient();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var baseId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.Users.Add(new AppUser { Id = userId, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) });
            db.CandidateProfiles.Add(new CandidateProfile { Id = Guid.NewGuid(), UserId = userId, FullName = "Ana Souza Lima", Skills = ["C#", "SQL"] });
            db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Currículo base" });
            db.ResumeVersions.Add(new ResumeVersion
            {
                Id = baseId,
                UserId = userId,
                ResumeDocumentId = documentId,
                Title = ResumeQueryService.BaseVersionTitle,
                PlainText = "Ana Souza Lima",
                Sections =
                [
                    new ResumeSection { Id = Guid.NewGuid(), UserId = userId, ResumeVersionId = baseId, SectionType = "skills", Title = "Competencias", SortOrder = 30, ContentJson = "{\"items\":[\"C#\",\".NET\",\"SQL\",\"Docker\",\"Git\"]}" },
                    new ResumeSection { Id = Guid.NewGuid(), UserId = userId, ResumeVersionId = baseId, SectionType = "experience", Title = "Experiencia", SortOrder = 40, ContentJson = "{\"items\":[{\"company\":\"Acme\",\"role\":\"Desenvolvedora\",\"period\":\"2021 - 2024\",\"description\":\"Criou APIs em C# com SQL.\"}]}" }
                ]
            });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (client, userId, baseId);
    }

    [Fact]
    public async Task Quick_FromPastedText_UsesLatestBaseAndFillsHistory()
    {
        var (client, _, baseId) = await SignInWithBaseResumeAsync();

        var response = await client.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest(Posting, "https://empresa.example/vaga/1", UseAi: false));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QuickTailorResponse>();

        Assert.NotNull(body);
        Assert.Equal(baseId, body.BaseResumeVersionId);
        var item = Assert.Single(body.Items);
        Assert.True(item.Succeeded, item.Error);
        Assert.Equal("Horizonte Pagamentos", item.Target!.Company);
        Assert.Equal("https://empresa.example/vaga/1", item.Target.SourceUrl);
        Assert.Contains("Desenvolvedora - Acme | 2021 - 2024", item.Tailored!.PlainText);

        var history = await client.GetFromJsonAsync<ApplicationHistoryItem[]>("/api/applications/history");
        var entry = Assert.Single(history!);
        Assert.False(entry.Applied);
        Assert.Equal("Horizonte Pagamentos", entry.Company);
        Assert.Equal("https://empresa.example/vaga/1", entry.SourceUrl);

        var mark = await client.PutAsJsonAsync($"/api/applications/{entry.ResumeVersionId}/applied", new SetApplicationStatusRequest(true));
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);
        history = await client.GetFromJsonAsync<ApplicationHistoryItem[]>("/api/applications/history");
        Assert.True(Assert.Single(history!).Applied);
    }

    [Fact]
    public async Task History_ShowsOneEntryPerJobLinkAndKeepsTheSentMark()
    {
        var (client, _, _) = await SignInWithBaseResumeAsync();
        const string link = "https://empresa.example/vaga/9";
        await client.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest(Posting, link, UseAi: false));
        var first = (await client.GetFromJsonAsync<ApplicationHistoryItem[]>("/api/applications/history"))![0];
        await client.PutAsJsonAsync($"/api/applications/{first.ResumeVersionId}/applied", new SetApplicationStatusRequest(true));

        await client.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest(Posting, link.ToUpperInvariant().Replace("HTTPS", "https"), UseAi: false));
        var history = await client.GetFromJsonAsync<ApplicationHistoryItem[]>("/api/applications/history");

        var entry = Assert.Single(history!);
        Assert.True(entry.Applied);
        Assert.NotEqual(first.ResumeVersionId, entry.ResumeVersionId);
    }

    [Fact]
    public async Task Quick_WithManyLinks_ReportsEachFailureWithoutFailingTheRest()
    {
        var (client, _, _) = await SignInWithBaseResumeAsync();

        var response = await client.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest("http://127.0.0.1/vaga-1\nhttp://10.0.0.5/vaga-2", UseAi: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QuickTailorResponse>();
        Assert.Equal(2, body!.Items.Length);
        Assert.All(body.Items, item =>
        {
            Assert.False(item.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(item.Error));
        });
    }

    [Fact]
    public async Task Quick_WithoutImportedResume_AsksToImportFirst()
    {
        var email = $"empty-{Guid.NewGuid():N}@jobpilot.test";
        const string password = "quick-test-password";
        var client = fixture.Factory.CreateClient();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
            db.Users.Add(new AppUser { Id = Guid.NewGuid(), Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) });
            await db.SaveChangesAsync();
        }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);

        var response = await client.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest(Posting, UseAi: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Importe seu currículo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SetApplied_OnAnotherUsersResume_IsNotFound()
    {
        var (owner, _, _) = await SignInWithBaseResumeAsync();
        await owner.PostAsJsonAsync("/api/applications/quick", new QuickTailorRequest(Posting, UseAi: false));
        var versionId = (await owner.GetFromJsonAsync<ApplicationHistoryItem[]>("/api/applications/history"))![0].ResumeVersionId;

        var (intruder, _, _) = await SignInWithBaseResumeAsync();
        var response = await intruder.PutAsJsonAsync($"/api/applications/{versionId}/applied", new SetApplicationStatusRequest(true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
