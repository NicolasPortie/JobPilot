namespace JobPilot.Api.Domain;

public class AiProvider
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string ProviderType { get; set; } = "kimi";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiFormat { get; set; } = "anthropic-messages";
    public string EncryptedApiKey { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(AiProvider input)
    {
        Name = input.Name;
        ProviderType = input.ProviderType;
        BaseUrl = input.BaseUrl;
        Model = input.Model;
        ApiFormat = input.ApiFormat;
        IsActive = input.IsActive;
        IsDefault = input.IsDefault;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
