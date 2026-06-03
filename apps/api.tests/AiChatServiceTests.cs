using System.Net;
using System.Text.Json;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPilot.Api.Tests;

public class AiChatServiceTests
{
    [Fact]
    public async Task AnalyzeResumeProfileAsync_OpenAiCompatible_EnviaJsonSchemaResponseFormat()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-secret-key-with-enough-length-32"
            })
            .Build();
        var provider = new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "OpenAI compatible",
            ProviderType = "openai",
            BaseUrl = "https://ai.example.test",
            Model = "gpt-test",
            ApiFormat = "openai-chat-completions",
            IsActive = true,
            IsDefault = true,
            EncryptedApiKey = SecretProtector.Protect("test-key", configuration)
        };
        db.AiProviders.Add(provider);
        await db.SaveChangesAsync();

        string requestBody = "";
        var http = new HttpClient(new CaptureHandler(request =>
        {
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "choices": [
                        {
                          "message": {
                            "role": "assistant",
                            "content": "{\"fullName\":\"Nicolas Portie\",\"email\":\"nicolas@example.com\",\"phone\":\"\",\"city\":\"\",\"country\":\"Brazil\",\"linkedinUrl\":\"\",\"githubUrl\":\"\",\"portfolioUrl\":\"\",\"professionalSummary\":\"Desenvolvedor .NET\",\"skills\":[\"C#\"],\"targetRole\":\"Desenvolvedor .NET\",\"seniority\":\"junior\",\"answers\":[]}"
                          }
                        }
                      ]
                    }
                    """)
            };
        }));
        var service = new AiChatService(http, configuration, db, NullLogger<AiChatService>.Instance, new AnswerResolver(db), new ResumeTextService());

        var result = await service.AnalyzeResumeProfileAsync(userId, "Nicolas Portie C# .NET", "", null);

        Assert.NotNull(result);
        using var json = JsonDocument.Parse(requestBody);
        var responseFormat = json.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
        Assert.Equal("jobpilot_structured_response", responseFormat.GetProperty("json_schema").GetProperty("name").GetString());
        Assert.True(responseFormat.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.True(responseFormat.GetProperty("json_schema").TryGetProperty("schema", out var schema));
        Assert.True(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public async Task ExtractJobRequirementsFromAttachmentsAsync_OpenAiResponses_EnviaImagemERegistraUso()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-secret-key-with-enough-length-32"
            })
            .Build();
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "OpenAI Responses",
            ProviderType = "openai",
            BaseUrl = "https://api.openai.test/v1",
            Model = "gpt-4o-mini",
            ApiFormat = "openai-responses",
            IsActive = true,
            IsDefault = true,
            EncryptedApiKey = SecretProtector.Protect("test-key", configuration)
        });
        await db.SaveChangesAsync();

        string requestBody = "";
        var http = new HttpClient(new CaptureHandler(request =>
        {
            requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "output": [
                        {
                          "type": "message",
                          "content": [
                            {
                              "type": "output_text",
                              "text": "{\"title\":\"Dev .NET\",\"company\":\"Acme\",\"location\":\"Remoto\",\"seniority\":\"junior\",\"workplace\":\"remote\",\"summary\":\"Vaga .NET\",\"requirements\":[{\"category\":\"technology\",\"label\":\"ASP.NET Core\",\"importance\":\"must_have\",\"evidenceText\":\"ASP.NET Core\",\"normalizedKeyword\":\"asp.net-core\",\"exactPhrases\":[\"ASP.NET Core\"]}],\"warnings\":[],\"confidence\":0.9}"
                            }
                          ]
                        }
                      ],
                      "usage": { "input_tokens": 1000, "output_tokens": 500, "total_tokens": 1500 }
                    }
                    """)
            };
        }));
        var service = new AiChatService(http, configuration, db, NullLogger<AiChatService>.Instance, new AnswerResolver(db), new ResumeTextService());

        var result = await service.ExtractJobRequirementsFromAttachmentsAsync(
            userId,
            new JobRequirementExtractionInput("", "", "", "", ""),
            [new AiInputAttachment("image_url", "image/png", "data:image/png;base64,AAAA", "vaga.png")]);

        Assert.NotNull(result);
        using var json = JsonDocument.Parse(requestBody);
        var content = json.RootElement.GetProperty("input")[1].GetProperty("content");
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.Equal("data:image/png;base64,AAAA", content[1].GetProperty("image_url").GetString());
        var usage = await db.AiUsageLogs.SingleAsync(x => x.UserId == userId);
        Assert.Equal("job_requirement_vision", usage.Operation);
        Assert.Equal(1000, usage.InputTokens);
        Assert.Equal(500, usage.OutputTokens);
        Assert.True(usage.EstimatedCostUsd > 0m);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
