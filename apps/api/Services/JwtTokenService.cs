using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace JobPilot.Api.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration) => _configuration = configuration;

    public string CreateToken(Guid subjectId, string name, string role, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        if (userId.HasValue) claims.Add(new("user_id", userId.Value.ToString()));

        var jwtKey = _configuration["Auth:JwtKey"] ?? "dev-only-change-this-key-please-32";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken("JobPilot", "JobPilot", claims, expires: DateTime.UtcNow.AddDays(30), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> CreateAgentTokenAsync(JobPilotDbContext db, Guid userId, string name)
    {
        var device = new AgentDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Status = "active"
        };
        db.AgentDevices.Add(device);
        await db.SaveChangesAsync();
        return CreateToken(device.Id, device.Name, "agent", userId);
    }
}
