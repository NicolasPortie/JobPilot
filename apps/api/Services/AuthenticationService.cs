using System.Security.Cryptography;
using System.Text;
using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;

namespace JobPilot.Api.Services;

public sealed class AuthenticationService(
    IRepository<AppUser> users,
    IRepository<RefreshToken> refreshTokens,
    JwtTokenService tokens,
    IUnitOfWork unitOfWork) : IAuthenticationService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaxSessionLifetime = TimeSpan.FromDays(30);

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email);
        if (user is null)
            return ServiceResult<AuthResponse>.Invalid("E-mail ou senha invalidos.");

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            return ServiceResult<AuthResponse>.Invalid("Conta temporariamente bloqueada por tentativas inválidas. Aguarde e tente novamente.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockoutEnd = DateTimeOffset.UtcNow.Add(LockoutDuration);
            await unitOfWork.SaveChangesAsync();
            return ServiceResult<AuthResponse>.Invalid("E-mail ou senha invalidos.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        var refresh = await IssueRefreshAsync(user.Id);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AuthResponse>.Ok(
            new AuthResponse(tokens.CreateToken(user.Id, user.Email, "user"), user.Email, refresh));
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return ServiceResult<AuthResponse>.Invalid("Sessão inválida. Entre novamente.");

        var stored = await refreshTokens.FirstOrDefaultAsync(x => x.TokenHash == Hash(refreshToken));
        if (stored is null || stored.RevokedAt.HasValue || stored.ExpiresAt <= DateTimeOffset.UtcNow)
            return ServiceResult<AuthResponse>.Invalid("Sessão expirada. Entre novamente.");

        if (stored.RootCreatedAt + MaxSessionLifetime <= DateTimeOffset.UtcNow)
            return ServiceResult<AuthResponse>.Invalid("Sessão atingiu o tempo máximo. Entre novamente.");

        var user = await users.FirstOrDefaultAsync(candidate => candidate.Id == stored.UserId);
        if (user is null)
            return ServiceResult<AuthResponse>.Invalid("Sessão inválida. Entre novamente.");

        stored.RevokedAt = DateTimeOffset.UtcNow;
        var next = await IssueRefreshAsync(user.Id, stored.RootCreatedAt);
        await unitOfWork.SaveChangesAsync();
        return ServiceResult<AuthResponse>.Ok(
            new AuthResponse(tokens.CreateToken(user.Id, user.Email, "user"), user.Email, next));
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var stored = await refreshTokens.FirstOrDefaultAsync(x => x.TokenHash == Hash(refreshToken));
        if (stored is null || stored.RevokedAt.HasValue) return;
        stored.RevokedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync();
    }

    private async Task<string> IssueRefreshAsync(Guid userId, DateTimeOffset? rootCreatedAt = null)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var now = DateTimeOffset.UtcNow;
        refreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAt = now.Add(RefreshLifetime),
            CreatedAt = now,
            RootCreatedAt = rootCreatedAt ?? now,
        });
        await Task.CompletedTask;
        return raw;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
