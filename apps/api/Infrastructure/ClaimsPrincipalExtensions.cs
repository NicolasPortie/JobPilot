using System.Security.Claims;
namespace JobPilot.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("Missing user id claim."));

    public static Guid AgentUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("user_id");
        return userId is not null ? Guid.Parse(userId) : principal.UserId();
    }
}
