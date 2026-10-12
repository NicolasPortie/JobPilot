namespace JobPilot.Api.Application.Contracts;

public record AuthResponse(string Token, string Email, string? RefreshToken = null);

public record RefreshRequest(string RefreshToken);
