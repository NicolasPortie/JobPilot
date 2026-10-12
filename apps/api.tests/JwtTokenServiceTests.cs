using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JobPilot.Api.Data;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_ReturnsNonEmptyToken()
    {
        var service = CreateService();

        var token = service.CreateToken(Guid.NewGuid(), "Test User", "user");

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void CreateToken_ContainsExpectedClaims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var token = service.CreateToken(userId, "Test User", "admin", userId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("Test User", jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == "user_id").Value);
    }

    [Fact]
    public void CreateToken_CanBeValidated()
    {
        var service = CreateService();
        var token = service.CreateToken(Guid.NewGuid(), "Test User", "user");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("JobPilot", jwt.Issuer);
        Assert.Equal("JobPilot", jwt.Audiences.First());
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddMinutes(50));
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddHours(1).AddMinutes(5));
    }

    private static JwtTokenService CreateService()
    {
        var db = CreateDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters-long"
            })
            .Build();
        return TestServices.JwtTokenService(db, config);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
