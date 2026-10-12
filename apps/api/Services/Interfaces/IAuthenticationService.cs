using JobPilot.Api.Application;

namespace JobPilot.Api.Services;

public interface IAuthenticationService
{
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult<AuthResponse>> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}
