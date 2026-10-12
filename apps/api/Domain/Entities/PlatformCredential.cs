namespace JobPilot.Api.Domain;

public class PlatformCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Platform { get; set; } = ""; // e.g. "linkedin", "indeed"
    public string AccountEmail { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(PlatformCredential input)
    {
        Platform = input.Platform;
        AccountEmail = input.AccountEmail;
        IsActive = input.IsActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
