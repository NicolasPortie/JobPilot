using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", async (LoginRequest request, JobPilotDbContext db, JwtTokenService tokens) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == request.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthResponse(tokens.CreateToken(user.Id, user.Email, "user"), user.Email));
        });

        return api;
    }
}
