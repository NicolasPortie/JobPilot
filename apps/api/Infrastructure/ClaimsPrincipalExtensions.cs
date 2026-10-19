using System.Security.Claims;
namespace JobPilot.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("Missing user id claim."));
}
