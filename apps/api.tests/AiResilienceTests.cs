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
}
