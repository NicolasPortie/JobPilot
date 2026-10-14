using System.Security.Cryptography;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed partial class AiCompletionService
{
    /// <summary>
    /// Escolhe o provedor de IA: configuração local, provedor salvo legível e, por último,
    /// o Ollama local. Chave salva com um segredo antigo é ignorada com aviso em vez
    /// de derrubar toda chamada de IA.
    /// </summary>
    public async Task<AiProvider> ResolveProviderAsync(Guid userId)
    {
        var configured = ConfiguredProvider();
        if (configured is not null) return configured;

        var saved = await db.AiProviders
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

        foreach (var provider in saved.Where(x => !IsOllama(x)))
        {
            if (TryReadKey(provider, out var key) && !string.IsNullOrWhiteSpace(key))
                return provider;
            logger.LogWarning("AI provider {ProviderName} ignored: saved API key cannot be read with the current Security:SecretKey.", provider.Name);
        }

        var localEnabled = configuration.GetValue("LocalLlm:Enabled", true);
        var savedOllama = saved.FirstOrDefault(IsOllama);
        if (localEnabled && savedOllama is not null) return savedOllama;
        if (localEnabled) return LocalOllamaProvider();

        return new AiProvider
        {
            Name = "Kimi Code",
            ProviderType = "kimi",
            BaseUrl = configuration["Kimi:BaseUrl"] ?? "https://api.kimi.com/coding",
            Model = configuration["Kimi:Model"] ?? "kimi-for-coding",
            ApiFormat = configuration["Kimi:Api"] ?? "anthropic-messages",
            EncryptedApiKey = ""
        };
    }

    /// <summary>Estado atual da IA para a interface, sem expor segredos.</summary>
    public async Task<AiStatusResponse> GetStatusAsync(Guid userId)
    {
        var provider = await ResolveProviderAsync(userId);
        var configured = IsOllama(provider) ||
            TryReadKey(provider, out var key) && !string.IsNullOrWhiteSpace(key);
        var last = health?.Snapshot(userId, HealthIdentity(provider));
        var sameProvider = last is not null;
        bool? lastOk = sameProvider ? last!.LastSucceeded : null;
        var message = !configured
            ? "Nenhuma chave de IA configurada. Defina Kimi:ApiKey ou Ai:Provider e Ai:ApiKey na configuração local."
            : lastOk == false
                ? $"A última chamada falhou: {last!.LastError}"
                : IsOllama(provider)
                    ? "Usando o Ollama local. Se ele não estiver rodando, o JobPilot usa o modo sem IA."
                    : lastOk == true ? "IA respondendo." : "IA configurada; conexão ainda não verificada.";
        return new AiStatusResponse(configured && lastOk != false, configured, provider.Name, provider.Model, lastOk, sameProvider ? last!.CheckedAt : null, message);
    }

    /// <summary>
    /// Provedor definido na configuração local. <c>Ai:Provider</c> aceita
    /// <c>anthropic</c>, <c>openai</c> ou <c>kimi</c>; as chaves antigas <c>Kimi:*</c>
    /// têm precedência sobre <c>Ai:ApiKey</c>.
    /// </summary>
    private AiProvider? ConfiguredProvider()
    {
        var kimiKey = FirstNonEmpty(
            configuration["Kimi:ApiKey"],
            configuration["KIMI_API_KEY"] ?? Environment.GetEnvironmentVariable("KIMI_API_KEY") ??
                Environment.GetEnvironmentVariable("KIMI_API_KEY", EnvironmentVariableTarget.User));
        if (!string.IsNullOrWhiteSpace(kimiKey)) return new AiProvider
        {
            Name = "Kimi Code",
            ProviderType = "kimi",
            BaseUrl = configuration["Kimi:BaseUrl"] ?? "https://api.kimi.com/coding",
            Model = configuration["Kimi:Model"] ?? "kimi-for-coding",
            ApiFormat = configuration["Kimi:Api"] ?? "anthropic-messages",
            EncryptedApiKey = SecretProtector.Protect(kimiKey, configuration)
        };

        var type = (configuration["Ai:Provider"] ?? "").Trim().ToLowerInvariant();
        var genericKey = configuration["Ai:ApiKey"];
        if (!string.IsNullOrWhiteSpace(genericKey) && type is "anthropic" or "openai" or "kimi")
        {
            var (name, baseUrl, model, format) = type switch
            {
                "anthropic" => ("Anthropic", "https://api.anthropic.com", "claude-haiku-5-5", "anthropic-messages"),
                "openai" => ("OpenAI", "https://api.openai.com/v1", "gpt-4.1-mini", "openai-chat-completions"),
                _ => ("Kimi Code", "https://api.kimi.com/coding", "kimi-for-coding", "anthropic-messages")
            };
            return new AiProvider
            {
                Name = name,
                ProviderType = type,
                BaseUrl = FirstNonEmpty(configuration["Ai:BaseUrl"]) ?? baseUrl,
                Model = FirstNonEmpty(configuration["Ai:Model"]) ?? model,
                ApiFormat = FirstNonEmpty(configuration["Ai:ApiFormat"]) ?? format,
                EncryptedApiKey = SecretProtector.Protect(genericKey, configuration)
            };
        }
        return null;
    }

    private bool TryReadKey(AiProvider provider, out string key)
    {
        try { return SecretProtector.TryUnprotect(provider.EncryptedApiKey, configuration, out key, out _); }
        catch (FormatException) { key = ""; return false; }
    }

    private string HealthIdentity(AiProvider provider)
    {
        TryReadKey(provider, out var key);
        var fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key ?? "")));
        return $"{provider.Id}|{provider.ProviderType}|{provider.BaseUrl}|{provider.Model}|{provider.ApiFormat}|{fingerprint}";
    }

    private AiProvider LocalOllamaProvider() => new()
    {
        Name = "Ollama Local",
        ProviderType = "ollama",
        BaseUrl = configuration["LocalLlm:BaseUrl"] ?? "http://localhost:11434",
        Model = configuration["LocalLlm:Model"] ?? "qwen3:4b-instruct",
        ApiFormat = "ollama-chat",
        EncryptedApiKey = ""
    };

    private static bool IsOllama(AiProvider provider) =>
        provider.ApiFormat == "ollama-chat" || provider.ProviderType == "ollama";

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

}
