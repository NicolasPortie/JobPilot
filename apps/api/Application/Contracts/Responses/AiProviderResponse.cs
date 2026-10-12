namespace JobPilot.Api.Application.Contracts;

public record AiProviderResponse(Guid Id, string Name, string ProviderType, string BaseUrl, string Model, string ApiFormat, bool HasApiKey, bool IsActive, bool IsDefault, DateTimeOffset UpdatedAt);
