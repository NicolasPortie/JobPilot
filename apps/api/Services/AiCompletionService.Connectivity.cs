using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public sealed partial class AiCompletionService
{
    public async Task<AiProviderConnectivityResult> TestProviderConnectivityAsync(AiProvider provider, Guid userId)
    {
        const string operation = "provider_test";
        const string system = "Return a short health-check response.";
        const string prompt = "Say only: ok";

        try
        {
            AiProviderEndpointPolicy.Validate(provider, configuration);
            string? text;
            if (provider.ApiFormat == "ollama-chat" || provider.ProviderType == "ollama")
            {
                text = await CompleteOllamaChatAsync(provider, system, prompt, null);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return BuildConnectivityResult(text, provider.Model);
            }

            var apiKey = SecretProtector.Unprotect(provider.EncryptedApiKey, configuration);
            if (string.IsNullOrWhiteSpace(apiKey))
                return new(false, "failed", provider.Model, "Chave da API não configurada.");

            if (provider.ApiFormat == "anthropic-messages")
            {
                text = await CompleteAnthropicMessagesAsync(provider, apiKey, system, prompt);
                await RecordEstimatedUsageAsync(userId, provider, operation, system, prompt, text);
                return BuildConnectivityResult(text, provider.Model);
            }

            if (provider.ApiFormat == "openai-responses")
            {
                text = await CompleteOpenAiResponsesAsync(provider, userId, operation, apiKey, system, prompt, null, []);
                return BuildConnectivityResult(text, provider.Model);
            }

            text = await CompleteOpenAiCompatibleAsync(provider, userId, operation, apiKey, system, prompt, null);
            return BuildConnectivityResult(text, provider.Model);
        }
        catch (Exception ex)
        {
            logger.LogWarning("AI connectivity test failed ({ErrorType}): {Reason}.", ex.GetType().Name, DescribeProviderError(ex));
            return new(false, "failed", provider.Model, DescribeProviderError(ex));
        }
    }

}
