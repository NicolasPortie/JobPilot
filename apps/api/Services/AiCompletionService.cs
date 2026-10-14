using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed partial class AiCompletionService(HttpClient http, IConfiguration configuration, JobPilotDbContext db, ILogger<AiCompletionService> logger, TraceRedactionService redaction, AiHealthTracker? health = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxPromptChars = 50000;
    // Respostas estruturadas (requisitos da vaga, currículo completo) passam de 2-3 mil tokens.
    private const int MaxOutputTokens = 8192;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(800);
    public async Task<string?> CompleteTextAsync(Guid userId, string operation, string system, string prompt, object? format = null)
    {
        var provider = await ResolveProviderAsync(userId);
        return await CompleteTextWithProviderAsync(provider, userId, operation, system, prompt, format);
    }

    public async Task<string?> CompleteTextWithProviderAsync(AiProvider provider, Guid userId, string operation, string system, string prompt, object? format = null, AiInputAttachment[]? attachments = null)
    {
        try
        {
            AiProviderEndpointPolicy.Validate(provider, configuration);
            system = redaction.RedactPii(Truncate(system));
            prompt = redaction.RedactPii(Truncate(prompt));
            attachments ??= [];
            string? text;
            if (provider.ApiFormat == "ollama-chat" || provider.ProviderType == "ollama")
            {
                text = await CompleteOllamaChatAsync(provider, system, prompt, format);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return ReportOutcome(userId, provider, text);
            }

            var apiKey = SecretProtector.Unprotect(provider.EncryptedApiKey, configuration);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                health?.RecordFailure(userId, HealthIdentity(provider), "Chave da API não configurada.");
                return null;
            }

            if (provider.ApiFormat == "anthropic-messages")
            {
                text = await CompleteAnthropicMessagesAsync(provider, apiKey, system, prompt);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return ReportOutcome(userId, provider, text);
            }
            if (provider.ApiFormat == "openai-responses")
                return ReportOutcome(userId, provider, await CompleteOpenAiResponsesAsync(provider, userId, operation, apiKey, system, prompt, format, attachments));
            return ReportOutcome(userId, provider, await CompleteOpenAiCompatibleAsync(provider, userId, operation, apiKey, system, prompt, format));
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI completion failed ({ErrorType}): {Reason}.", ex.GetType().Name, DescribeProviderError(ex));
            health?.RecordFailure(userId, HealthIdentity(provider), DescribeProviderError(ex));
            return null;
        }
    }

    private string? ReportOutcome(Guid userId, AiProvider provider, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            health?.RecordFailure(userId, HealthIdentity(provider), "O provedor não retornou texto.");
        else
            health?.RecordSuccess(userId, HealthIdentity(provider));
        return text;
    }

    private static string Truncate(string value) =>
        value.Length > MaxPromptChars ? value[..MaxPromptChars] + "\n[truncado: prompt excedeu o limite]" : value;

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> buildRequest, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = buildRequest();
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            // Um timeout já consumiu o orçamento de espera: repetir só dobraria a demora.
            catch (HttpRequestException) when (attempt == 0)
            {
                await Task.Delay(RetryDelay, cancellationToken);
                continue;
            }

            var transient = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
            if (!transient || attempt > 0) return response;
            response.Dispose();
            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    private static AiProviderConnectivityResult BuildConnectivityResult(string? text, string model) =>
        string.IsNullOrWhiteSpace(text)
            ? new(false, "failed", model, "O provedor não retornou texto no teste.")
            : new(true, text.Trim(), model, null);

    private static string DescribeProviderError(Exception ex)
    {
        if (ex is CryptographicException)
            return "A chave salva não pode ser lida com o segredo atual da API. Edite o provedor e salve a chave novamente.";

        if (ex is HttpRequestException http && http.StatusCode is not null)
            return $"O provedor recusou a chamada (HTTP {(int)http.StatusCode}).";

        return ex is OperationCanceledException
            ? "O provedor não respondeu dentro do prazo."
            : "Não foi possível concluir a chamada ao provedor.";
    }

    public async Task<string> ActiveModelNameAsync(Guid userId)
    {
        var provider = await ResolveProviderAsync(userId);
        return provider.Model;
    }

    private async Task<string?> CompleteAnthropicMessagesAsync(AiProvider provider, string apiKey, string system, string prompt)
    {
        using var response = await SendWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Headers.UserAgent.ParseAdd("jobpilot/0.1.0");
            request.Content = JsonContent.Create(new AnthropicMessagesRequest(provider.Model, system, [new AnthropicMessage("user", prompt)], MaxOutputTokens));
            return request;
        });
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadFromJsonAsync<AnthropicMessagesResponse>(JsonOptions);
        return string.Join("", body?.Content.Where(x => x.Type == "text").Select(x => x.Text) ?? []).Trim();
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Task.CompletedTask;
    }

    private async Task<string?> CompleteOpenAiResponsesAsync(AiProvider provider, Guid userId, string operation, string apiKey, string system, string prompt, object? format, AiInputAttachment[] attachments)
    {
        var userContent = BuildOpenAiResponsesUserContent(prompt, attachments);
        var input = new List<OpenAiResponseInputItem>
        {
            new("system", system),
            new("user", userContent)
        };
        var body = format is null
            ? (object)new OpenAiResponsesRequest(provider.Model, input, null)
            : new OpenAiStructuredResponsesRequest(provider.Model, input, new OpenAiTextFormat(new OpenAiTextFormatJsonSchema("jobpilot_structured_response", true, format)));
        using var response = await SendWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("OpenAI-Beta", "responses=v1");
            request.Content = JsonContent.Create(body, options: JsonOptions);
            return request;
        });
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<OpenAiResponsesResponse>(JsonOptions);
        await RecordUsageAsync(userId, provider, operation, result?.Usage);
        return result?.Output.FirstOrDefault(x => x.Type == "message")?.Content.FirstOrDefault(x => x.Type == "output_text")?.Text?.Trim();
    }

    private async Task<string?> CompleteOpenAiCompatibleAsync(AiProvider provider, Guid userId, string operation, string apiKey, string system, string prompt, object? format)
    {
        var messages = new[] { new ChatMessage("system", system), new ChatMessage("user", prompt) };
        using var response = await SendWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = format is null
                ? JsonContent.Create(new ChatCompletionRequest(provider.Model, messages, MaxOutputTokens))
                : JsonContent.Create(new StructuredChatCompletionRequest(
                    provider.Model,
                    messages,
                    MaxOutputTokens,
                    new ChatResponseFormat(
                        "json_schema",
                        new ChatJsonSchema("jobpilot_structured_response", true, format))));
            return request;
        });
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions);
        await RecordUsageAsync(userId, provider, operation, body?.Usage);
        return body?.Choices.FirstOrDefault()?.Message.Content?.Trim();
    }

    private async Task<string?> CompleteOllamaChatAsync(AiProvider provider, string system, string prompt, object? format)
    {
        var noThink = provider.Model.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase) ? " /no_think" : "";
        using var response = await SendWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/api/chat");
            request.Content = JsonContent.Create(new OllamaChatRequest(
                provider.Model,
                [new OllamaMessage("system", system + noThink), new OllamaMessage("user", prompt + noThink)],
                false,
                false,
                format,
                new OllamaOptions(0m, 0.9m, 8192, 4096)));
            return request;
        });
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions);
        return StripThinking(body?.Message?.Content);
    }

    private static string? StripThinking(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var cleaned = text.Trim();
        var endThink = cleaned.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (endThink >= 0)
            cleaned = cleaned[(endThink + "</think>".Length)..].Trim();

        return cleaned;
    }

    private async Task RecordEstimatedUsageAsync(Guid userId, AiProvider provider, string operation, string system, string prompt, string? output)
    {
        var inputTokens = EstimateTokens(system) + EstimateTokens(prompt);
        var outputTokens = EstimateTokens(output ?? "");
        var totalTokens = inputTokens + outputTokens;
        if (totalTokens <= 0) return;

        var usage = new AiUsage(inputTokens, outputTokens, null, null, totalTokens);
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AiProviderId = provider.Id == Guid.Empty ? null : provider.Id,
            ProviderType = provider.ProviderType,
            ApiFormat = provider.ApiFormat,
            Model = provider.Model,
            Operation = operation,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = EstimateCostUsd(provider.Model, inputTokens, outputTokens),
            UsageJson = JsonSerializer.Serialize(new { estimated = true, inputTokens, outputTokens, totalTokens }, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static int EstimateTokens(string text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : Math.Max(1, (int)Math.Ceiling(text.Length / 4m));

    private static object BuildOpenAiResponsesUserContent(string prompt, AiInputAttachment[] attachments)
    {
        if (attachments.Length == 0) return prompt;

        var content = new List<OpenAiResponseContentInput>
        {
            new("input_text", prompt, null, null, null)
        };

        foreach (var attachment in attachments.Where(x => !string.IsNullOrWhiteSpace(x.DataUrl)))
        {
            var normalizedType = attachment.Type.Trim().ToLowerInvariant();
            if (normalizedType == "file" || attachment.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                content.Add(new OpenAiResponseContentInput("file", null, null, attachment.DataUrl, attachment.FileName));
            else
                content.Add(new OpenAiResponseContentInput("image_url", null, attachment.DataUrl, null, attachment.FileName));
        }

        return content;
    }

    private async Task RecordUsageAsync(Guid userId, AiProvider provider, string operation, AiUsage? usage)
    {
        if (usage is null) return;

        var inputTokens = usage.InputTokens ?? usage.PromptTokens ?? 0;
        var outputTokens = usage.OutputTokens ?? usage.CompletionTokens ?? 0;
        var totalTokens = usage.TotalTokens == 0 ? inputTokens + outputTokens : usage.TotalTokens;
        if (totalTokens <= 0) return;

        var cost = EstimateCostUsd(provider.Model, inputTokens, outputTokens);
        db.AiUsageLogs.Add(new AiUsageLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AiProviderId = provider.Id == Guid.Empty ? null : provider.Id,
            ProviderType = provider.ProviderType,
            ApiFormat = provider.ApiFormat,
            Model = provider.Model,
            Operation = operation,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            EstimatedCostUsd = cost,
            UsageJson = JsonSerializer.Serialize(usage, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private decimal EstimateCostUsd(string model, int inputTokens, int outputTokens)
    {
        var normalized = model.ToLowerInvariant();
        var inputPerMillion = configuration.GetValue<decimal?>($"AiCosts:{model}:InputPerMillionUsd");
        var outputPerMillion = configuration.GetValue<decimal?>($"AiCosts:{model}:OutputPerMillionUsd");

        if (inputPerMillion is null || outputPerMillion is null)
        {
            (inputPerMillion, outputPerMillion) = normalized switch
            {
                var value when value.Contains("gpt-4.1-mini") => (0.40m, 1.60m),
                var value when value.Contains("gpt-4.1") => (2.00m, 8.00m),
                var value when value.Contains("gpt-4o-mini") => (0.15m, 0.60m),
                var value when value.Contains("gpt-4o") => (2.50m, 10.00m),
                _ => (0m, 0m)
            };
        }

        return Math.Round((inputTokens / 1_000_000m * inputPerMillion.Value) + (outputTokens / 1_000_000m * outputPerMillion.Value), 6);
    }

    private sealed record ChatCompletionRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] ChatMessage[] Messages, [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens);
    private sealed record StructuredChatCompletionRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] ChatMessage[] Messages, [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens, [property: JsonPropertyName("response_format")] ChatResponseFormat ResponseFormat);
    private sealed record ChatResponseFormat([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("json_schema")] ChatJsonSchema JsonSchema);
    private sealed record ChatJsonSchema([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("strict")] bool Strict, [property: JsonPropertyName("schema")] object Schema);
    private sealed record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] List<ChatChoice> Choices, [property: JsonPropertyName("usage")] AiUsage? Usage);
    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);
    private sealed record AnthropicMessagesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("system")] string System, [property: JsonPropertyName("messages")] AnthropicMessage[] Messages, [property: JsonPropertyName("max_tokens")] int MaxTokens);
    private sealed record AnthropicMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record AnthropicMessagesResponse([property: JsonPropertyName("content")] List<AnthropicContent> Content);
    private sealed record AnthropicContent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);
    private sealed record OllamaChatRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] OllamaMessage[] Messages, [property: JsonPropertyName("stream")] bool Stream, [property: JsonPropertyName("think")] bool Think, [property: JsonPropertyName("format")] object? Format, [property: JsonPropertyName("options")] OllamaOptions Options);
    private sealed record OllamaMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record OllamaOptions([property: JsonPropertyName("temperature")] decimal Temperature, [property: JsonPropertyName("top_p")] decimal TopP, [property: JsonPropertyName("num_ctx")] int NumCtx, [property: JsonPropertyName("num_predict")] int NumPredict);
    private sealed record OllamaChatResponse([property: JsonPropertyName("message")] OllamaMessage? Message);

    private sealed record OpenAiResponsesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("input")] List<OpenAiResponseInputItem> Input, [property: JsonPropertyName("text")] OpenAiTextFormat? Text);
    private sealed record OpenAiStructuredResponsesRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("input")] List<OpenAiResponseInputItem> Input, [property: JsonPropertyName("text")] OpenAiTextFormat Text);
    private sealed record OpenAiResponseInputItem([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] object Content);
    private sealed record OpenAiResponseContentInput([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string? Text, [property: JsonPropertyName("image_url")] string? ImageUrl, [property: JsonPropertyName("file")] string? File, [property: JsonPropertyName("filename")] string? FileName);
    private sealed record OpenAiTextFormat([property: JsonPropertyName("format")] OpenAiTextFormatJsonSchema Format);
    private sealed record OpenAiTextFormatJsonSchema([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("strict")] bool Strict, [property: JsonPropertyName("schema")] object Schema);
    private sealed record OpenAiResponsesResponse([property: JsonPropertyName("output")] List<OpenAiResponseOutput> Output, [property: JsonPropertyName("usage")] AiUsage? Usage);
    private sealed record OpenAiResponseOutput([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("content")] List<OpenAiResponseContent> Content);
    private sealed record OpenAiResponseContent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);
    private sealed record AiUsage(
        [property: JsonPropertyName("input_tokens")] int? InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens,
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens);
}

public sealed record AiProviderConnectivityResult(bool Ok, string Sample, string Model, string? Error);
