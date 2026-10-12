using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia a autenticação dos usuários na aplicação.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthenticationService authentication, AccountDeletionService accountDeletion) : ControllerBase
{
    /// <summary>
    /// Autentica um usuário com as credenciais fornecidas.
    /// </summary>
    /// <param name="request">Credenciais de login.</param>
    /// <returns>Token de autenticação ou 401.</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await authentication.LoginAsync(request);
        return result.Status == ServiceResultStatus.Invalid
            ? Unauthorized(new ProblemDetails
            {
                Type = "https://jobpilot.io/errors/401",
                Title = "Credenciais invalidas",
                Status = StatusCodes.Status401Unauthorized,
                Detail = result.Error,
                Instance = $"{HttpContext.Request.Method} {HttpContext.Request.Path}"
            })
            : Ok(result.Value);
    }

    /// <summary>
    /// Renova a sessão com rotação do refresh token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var result = await authentication.RefreshAsync(request.RefreshToken);
        return result.Status == ServiceResultStatus.Invalid
            ? Unauthorized(new ProblemDetails
            {
                Type = "https://jobpilot.io/errors/401",
                Title = "Sessão expirada",
                Status = StatusCodes.Status401Unauthorized,
                Detail = result.Error,
                Instance = $"{HttpContext.Request.Method} {HttpContext.Request.Path}"
            })
            : Ok(result.Value);
    }

    /// <summary>
    /// Encerra a sessão revogando o refresh token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await authentication.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>
    /// Exclui a conta e todos os dados do usuário autenticado.
    /// </summary>
    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var removed = await accountDeletion.DeleteAccountAsync(User.UserId(), cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
