using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

/// <summary>
/// Configuração de IA editável na interface: um provedor salvo por usuário, com a chave criptografada.
/// O provedor salvo e ativo vence a configuração local (<c>Ai:*</c>) e o Ollama.
/// </summary>
public sealed class AiSettingsService(
    IRepository<AiProvider> providers,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    AiCompletionService completion)
{
    public static readonly AiProviderPreset[] Presets =
    [
        new("anthropic", "Anthropic (Claude)", "https://api.anthropic.com", "claude-haiku-5-5", true, "Chave criada no console da Anthropic."),
        new("openai", "OpenAI", "https://api.openai.com/v1", "gpt-4.1-mini", true, "Chave criada no painel da OpenAI."),
        new("openai-compatible", "Gateway compatível com OpenAI (OmniRoute etc.)", "", "", true,
            "Endereço terminado em /v1, em HTTPS. O host precisa estar em Ai:AllowedHosts na configuração do servidor."),
        new("kimi", "Kimi Code", "https://api.kimi.com/coding", "kimi-for-coding", true, "Algumas contas do Kimi Code recusam apps de terceiros (erro 403)."),
        new("ollama", "Ollama local", "http://localhost:11434", "qwen3:4b-instruct", false, "Roda na sua máquina e não precisa de chave.")
    ];

    public static bool IsKnownType(string? providerType) =>
        Presets.Any(x => x.ProviderType.Equals(providerType, StringComparison.OrdinalIgnoreCase));

    private static string FormatFor(string providerType) => providerType switch
    {
        "anthropic" or "kimi" => "anthropic-messages",
        "ollama" => "ollama-chat",
        _ => "openai-chat-completions"
    };

    public async Task<AiSettingsResponse> GetAsync(Guid userId)
    {
        var saved = await FindSavedAsync(userId);
        var active = await completion.ResolveProviderAsync(userId);
        var source = active.Id != Guid.Empty ? "saved" : active.ProviderType == "ollama" ? "ollama" : "config";
        var defaults = Presets[0];
        return saved is null
            ? new(defaults.ProviderType, defaults.Label, defaults.BaseUrl, defaults.Model, false, true, false, source, active.Name, Presets)
            : new(saved.ProviderType, saved.Name, saved.BaseUrl, saved.Model, !string.IsNullOrWhiteSpace(saved.EncryptedApiKey), saved.IsActive, true, source, active.Name, Presets);
    }

    public async Task<ServiceResult<AiSettingsResponse>> SaveAsync(Guid userId, AiSettingsRequest request)
    {
        var type = request.ProviderType.Trim().ToLowerInvariant();
        var preset = Presets.First(x => x.ProviderType == type);
        var saved = await FindSavedAsync(userId);
        var key = ResolveKey(saved, type, request);
        if (preset.RequiresApiKey && string.IsNullOrWhiteSpace(key))
            return ServiceResult<AiSettingsResponse>.Invalid("Informe a chave de API deste provedor.");

        var candidate = Build(type, request, key);
        AiProviderEndpointPolicy.Validate(candidate, configuration);

        if (saved is null)
        {
            candidate.Id = Guid.NewGuid();
            candidate.UserId = userId;
            providers.Add(candidate);
            saved = candidate;
        }
        else
        {
            saved.Apply(candidate);
            saved.EncryptedApiKey = candidate.EncryptedApiKey;
        }
        saved.IsDefault = true;
        foreach (var other in await providers.ListAsync(x => x.UserId == userId && x.Id != saved.Id && x.IsDefault))
            other.IsDefault = false;

        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AiSettingsResponse>.Ok(await GetAsync(userId));
    }

    public async Task<ServiceResult<AiSettingsResponse>> DeleteAsync(Guid userId)
    {
        var saved = await FindSavedAsync(userId);
        if (saved is null) return ServiceResult<AiSettingsResponse>.NotFound();
        providers.Remove(saved);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AiSettingsResponse>.NoContent();
    }

    /// <summary>Testa o que está na tela, sem salvar. Chave em branco usa a já salva, se o destino não mudou.</summary>
    public async Task<ServiceResult<AiProviderTestResponse>> TestAsync(Guid userId, AiSettingsRequest request)
    {
        var type = request.ProviderType.Trim().ToLowerInvariant();
        var preset = Presets.First(x => x.ProviderType == type);
        var key = ResolveKey(await FindSavedAsync(userId), type, request);
        if (preset.RequiresApiKey && string.IsNullOrWhiteSpace(key))
            return ServiceResult<AiProviderTestResponse>.Ok(new(false, request.Name, request.Model, "", "Informe a chave de API para testar."));

        var candidate = Build(type, request, key);
        try
        {
            AiProviderEndpointPolicy.Validate(candidate, configuration);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult<AiProviderTestResponse>.Ok(new(false, request.Name, request.Model, "", ex.Message));
        }

        var result = await completion.TestProviderConnectivityAsync(candidate, userId);
        return ServiceResult<AiProviderTestResponse>.Ok(new(result.Ok, candidate.Name, result.Model, result.Sample, result.Error));
    }

    private Task<AiProvider?> FindSavedAsync(Guid userId) =>
        providers.Query()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();

    private AiProvider Build(string type, AiSettingsRequest request, string key) => new()
    {
        Name = request.Name.Trim(),
        ProviderType = type,
        BaseUrl = request.BaseUrl.Trim().TrimEnd('/'),
        Model = request.Model.Trim(),
        ApiFormat = FormatFor(type),
        EncryptedApiKey = string.IsNullOrWhiteSpace(key) ? "" : SecretProtector.Protect(key, configuration),
        IsActive = request.Enabled,
        IsDefault = true
    };

    /// <summary>
    /// Chave nova da tela, ou a salva quando a tela veio em branco. A salva só é reaproveitada se o provedor
    /// e o host não mudaram: assim uma chave nunca é enviada a um destino que o usuário não escolheu com ela.
    /// </summary>
    private string ResolveKey(AiProvider? saved, string type, AiSettingsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) return request.ApiKey.Trim();
        if (saved is null || string.IsNullOrWhiteSpace(saved.EncryptedApiKey)) return "";
        if (!saved.ProviderType.Equals(type, StringComparison.OrdinalIgnoreCase) || !SameHost(saved.BaseUrl, request.BaseUrl)) return "";
        return SecretProtector.TryUnprotect(saved.EncryptedApiKey, configuration, out var key, out _) ? key : "";
    }

    private static bool SameHost(string a, string b) =>
        Uri.TryCreate(a.Trim(), UriKind.Absolute, out var left) &&
        Uri.TryCreate(b.Trim(), UriKind.Absolute, out var right) &&
        left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase);
}
