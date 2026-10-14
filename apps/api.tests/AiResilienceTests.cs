using System.Net;
using System.Text;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

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
            ["KIMI_API_KEY"] = "",
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
            ["KIMI_API_KEY"] = "",
            ["Ai:Provider"] = "anthropic",
            ["Ai:ApiKey"] = "sk-configured"
        }).Build();
        var health = new AiHealthTracker();
        var handler = new StaticHandler(HttpStatusCode.Forbidden, "{\"error\":{\"message\":\"plan has no access sk-leaked-secret\"}}");
        var service = new AiCompletionService(new HttpClient(handler), configuration, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService(), health);
        var userId = Guid.NewGuid();

        var text = await service.CompleteTextAsync(userId, "test", "system", "prompt");
        var status = await service.GetStatusAsync(userId);

        Assert.Null(text);
        Assert.False(status.Available);
        Assert.True(status.Configured);
        Assert.Contains("HTTP 403", status.Message);
        Assert.DoesNotContain("plan has no access", status.Message);
        Assert.DoesNotContain("sk-leaked-secret", status.Message);
        var other = await service.GetStatusAsync(Guid.NewGuid());
        Assert.True(other.Available);
        Assert.Null(other.LastCallSucceeded);
        Assert.Null(other.LastCheckedAt);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Kimi:ApiKey")]
    [InlineData("KIMI_API_KEY")]
    public async Task LocalKimiKey_TakesPrecedenceOverReadableSavedAndGenericKey(string keySetting)
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters",
            ["KIMI_API_KEY"] = "",
            [keySetting] = "local-test-key",
            ["Ai:Provider"] = "anthropic",
            ["Ai:ApiKey"] = "generic-test-key"
        }).Build();
        db.AiProviders.Add(new AiProvider { Id = Guid.NewGuid(), UserId = userId, Name = "Saved",
            ProviderType = "anthropic", BaseUrl = "https://api.anthropic.com", Model = "saved-model",
            IsActive = true, IsDefault = true, EncryptedApiKey = SecretProtector.Protect("saved-test-key", config) });
        await db.SaveChangesAsync();
        var service = new AiCompletionService(new HttpClient(), config, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService());
        var resolved = await service.ResolveProviderAsync(userId);
        Assert.Equal("kimi", resolved.ProviderType);
        Assert.Equal("local-test-key", SecretProtector.Unprotect(resolved.EncryptedApiKey, config));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnreadableSavedKey_IsSkipped_WithoutLocalConfiguration(bool malformed)
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters",
            ["KIMI_API_KEY"] = "", ["LocalLlm:Enabled"] = "false"
        }).Build();
        var oldConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Security:SecretKey"] = "old-secret-with-more-than-thirty-two-characters" }).Build();
        db.AiProviders.Add(new AiProvider { Id = Guid.NewGuid(), UserId = userId, Name = "Unreadable",
            IsActive = true, EncryptedApiKey = malformed ? "enc:not-valid-ciphertext" : SecretProtector.Protect("old-test-key", oldConfig) });
        await db.SaveChangesAsync();
        var logger = new CapturingLogger();
        var service = new AiCompletionService(new HttpClient(), config, db, logger, new TraceRedactionService());
        var status = await service.GetStatusAsync(userId);
        Assert.False(status.Configured);
        Assert.False(status.Available);
        Assert.Null(await service.CompleteTextAsync(userId, "test", "system", "prompt"));
        Assert.Contains(logger.Messages, message => message.Contains("ignored"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("old-test-key"));
    }

    [Fact]
    public async Task AnthropicRequest_HasEnoughTokens_AndTracksSuccessAndConfigurationChange()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters",
            ["Kimi:ApiKey"] = "test-key"
        }).Build();
        var handler = new StaticHandler(HttpStatusCode.OK, "{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}");
        var service = new AiCompletionService(new HttpClient(handler), config, db, NullLogger<AiCompletionService>.Instance, new TraceRedactionService(), new AiHealthTracker());
        Assert.Null((await service.GetStatusAsync(userId)).LastCallSucceeded);
        Assert.Equal("ok", await service.CompleteTextAsync(userId, "test", "system", "prompt"));
        using var body = System.Text.Json.JsonDocument.Parse(handler.LastBody!);
        Assert.True(body.RootElement.GetProperty("max_tokens").GetInt32() >= 8192);
        Assert.True((await service.GetStatusAsync(userId)).LastCallSucceeded);
        config["Kimi:ApiKey"] = "changed-test-key";
        Assert.Null((await service.GetStatusAsync(userId)).LastCallSucceeded);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class CapturingLogger : ILogger<AiCompletionService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
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
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
