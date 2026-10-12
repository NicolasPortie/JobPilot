namespace JobPilot.Api.Application.Contracts;

public record AiProviderRequest(string Name, string ProviderType, string BaseUrl, string Model, string ApiFormat, string? ApiKey, bool IsActive, bool IsDefault);
