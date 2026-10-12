namespace JobPilot.Api.Application.Contracts;

public record PlatformCredentialResponse(Guid Id, string Platform, string AccountEmail, bool HasPassword, bool IsActive, DateTimeOffset UpdatedAt);
