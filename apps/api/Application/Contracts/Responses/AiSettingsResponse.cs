namespace JobPilot.Api.Application.Contracts;

/// <summary>
/// Configuração de IA do usuário. Nunca devolve a chave: só se ela existe.
/// <c>ActiveSource</c> diz de onde vem a IA em uso: saved, config, ollama.
/// </summary>
public record AiSettingsResponse(
    string ProviderType,
    string Name,
    string BaseUrl,
    string Model,
    bool HasApiKey,
    bool Enabled,
    bool Saved,
    string ActiveSource,
    string ActiveProvider,
    AiProviderPreset[] Presets);

public record AiProviderPreset(string ProviderType, string Label, string BaseUrl, string Model, bool RequiresApiKey, string Hint);
