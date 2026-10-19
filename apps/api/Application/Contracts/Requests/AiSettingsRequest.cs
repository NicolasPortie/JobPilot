namespace JobPilot.Api.Application.Contracts;

/// <summary>Configuração de IA editada na tela. <c>ApiKey</c> vazia mantém a chave já salva.</summary>
public record AiSettingsRequest(string ProviderType, string Name, string BaseUrl, string Model, string? ApiKey, bool Enabled);
