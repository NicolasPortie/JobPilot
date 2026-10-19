using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobPilot.Api.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace JobPilot.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(Guid subjectId, string name, string role, Guid? userId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString()),
            new(ClaimTypes.NameIdentifier, subjectId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        if (userId.HasValue) claims.Add(new("user_id", userId.Value.ToString()));

        var jwtKey = SecurityConfiguration.JwtKey(configuration);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(SecurityConfiguration.Issuer, SecurityConfiguration.Audience, claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
