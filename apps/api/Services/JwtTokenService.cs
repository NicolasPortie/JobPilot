using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobPilot.Api.Configuration;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;

namespace JobPilot.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration, IRepository<AgentDevice> devices, IUnitOfWork unitOfWork)
{
    public string CreateToken(Guid subjectId, string name, string role, Guid? userId = null, bool legacyRuntime = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(ClaimTypes.NameIdentifier, subjectId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        if (userId.HasValue) claims.Add(new("user_id", userId.Value.ToString()));
        if (legacyRuntime) claims.Add(new("legacy_runtime", "enabled"));

        var jwtKey = SecurityConfiguration.JwtKey(configuration);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(SecurityConfiguration.Issuer, SecurityConfiguration.Audience, claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> CreateAgentTokenAsync(Guid userId, string name)
    {
        var device = new AgentDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Status = "active"
        };
        devices.Add(device);
        await unitOfWork.SaveChangesAsync();
        return CreateToken(device.Id, device.Name, "agent", userId);
    }
}
