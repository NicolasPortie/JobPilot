using System.Net;
using System.Text;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPilot.Api.Tests;

public sealed class AiResilienceTests
{
    [Fact]
    public void RedactPii_RemovesEmailPhoneAndCpf()
    {
        var redaction = new TraceRedactionService();
        var result = redaction.RedactPii("Contato joao.silva@example.com fone (11) 99999-1234 cpf 123.456.789-09");

        Assert.DoesNotContain("joao.silva@example.com", result);
        Assert.DoesNotContain("99999-1234", result);
        Assert.DoesNotContain("123.456.789-09", result);
        Assert.Contains("Contato", result);
    }

    [Fact]
    public async Task TransientFailure_RetriesOnce_AndRedactsPrompt()
    {
        await using var db = CreateDb();
        var handler = new FlakyHandler();
        var service = new AiCompletionService(
            new HttpClient(handler),
            new ConfigurationBuilder().Build(),
            db,
            NullLogger<AiCompletionService>.Instance,
            new TraceRedactionService());
        var provider = new AiProvider
        {
            Id = Guid.NewGuid(),
            Name = "Ollama Test",
            ProviderType = "ollama",
            BaseUrl = "http://localhost:11434",
            Model = "qwen3:4b-instruct",
            ApiFormat = "ollama-chat",
        };

        var longPrompt = "email joao@example.com telefone (11) 98888-7777 " + new string('x', 60000);
        var result = await service.CompleteTextWithProviderAsync(provider, Guid.NewGuid(), "test", "system", longPrompt);

        Assert.Equal("ok", result);
        Assert.Equal(2, handler.Calls);
        Assert.NotNull(handler.LastBody);
        Assert.DoesNotContain("joao@example.com", handler.LastBody);
        Assert.DoesNotContain("98888-7777", handler.LastBody);
        Assert.Contains("truncado", handler.LastBody);
        Assert.True(handler.LastBody.Length < longPrompt.Length);
    }

    [Fact]
    public async Task ResolveProvider_SkipsSavedKeyEncryptedWithOldSecret_AndUsesConfiguredKey()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var oldSecret = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "old-secret-old-secret-old-secret-0001"
        }).Build();
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Saved Kimi",
            ProviderType = "kimi",
            BaseUrl = "https://api.kimi.com/coding",
            Model = "kimi-for-coding",
            ApiFormat = "anthropic-messages",
            EncryptedApiKey = SecretProtector.Protect("sk-old", oldSecret),
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "new-secret-new-secret-new-secret-0002",
            ["Ai:Provider"] = "anthropic",
            ["Ai:ApiKey"] = "sk-configured"
        }).Build();
        var service = new AiCompletionService(new HttpClient(), configuration, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService());

        var provider = await service.ResolveProviderAsync(userId);

        Assert.Equal("anthropic", provider.ProviderType);
        Assert.Equal("https://api.anthropic.com", provider.BaseUrl);
        Assert.Equal("sk-configured", SecretProtector.Unprotect(provider.EncryptedApiKey, configuration));
    }

    [Fact]
    public async Task ProviderRejection_IsReportedInStatus()
    {
        await using var db = CreateDb();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "new-secret-new-secret-new-secret-0002",
            ["Ai:Provider"] = "anthropic",
            ["Ai:ApiKey"] = "sk-configured"
        }).Build();
        var health = new AiHealthTracker();
        var handler = new StaticHandler(HttpStatusCode.Forbidden, "{\"error\":{\"message\":\"plan has no access\"}}");
        var service = new AiCompletionService(new HttpClient(handler), configuration, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService(), health);
        var userId = Guid.NewGuid();

        var text = await service.CompleteTextAsync(userId, "test", "system", "prompt");
        var status = await service.GetStatusAsync(userId);

        Assert.Null(text);
        Assert.False(status.Available);
        Assert.True(status.Configured);
        Assert.Contains("plan has no access", status.Message);
        Assert.Equal(1, handler.Calls);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class FlakyHandler : HttpMessageHandler
    {
        public int Calls;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Calls == 1) throw new HttpRequestException("transient boom");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StaticHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
