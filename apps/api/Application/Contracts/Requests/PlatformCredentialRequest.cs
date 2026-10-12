namespace JobPilot.Api.Application.Contracts;

public record PlatformCredentialRequest(string Platform, string AccountEmail, string Password, bool IsActive);
