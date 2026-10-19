using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsOkWithToken()
    {
        await using var db = CreateDb();
        var users = new EfRepository<AppUser>(db);
        var userId = Guid.NewGuid();
        var password = "correct-password";
        db.Users.Add(new AppUser
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters-long"
            })
            .Build();
        var tokens = TestServices.JwtTokenService(db, config);
        var service = new AuthenticationService(users, TestServices.Repo<RefreshToken>(db), tokens, TestServices.Uow(db));

        var result = await service.LoginAsync(new LoginRequest("user@example.com", password));

        Assert.Equal(ServiceResultStatus.Ok, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("user@example.com", result.Value.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));
    }

    [Fact]
    public async Task LoginAsync_NormalizesEmailCaseAndWhitespace()
    {
        await using var db = CreateDb();
        var users = new EfRepository<AppUser>(db);
        var password = "correct-password";
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters-long"
            })
            .Build();
        var tokens = TestServices.JwtTokenService(db, config);
        var service = new AuthenticationService(users, TestServices.Repo<RefreshToken>(db), tokens, TestServices.Uow(db));

        var result = await service.LoginAsync(new LoginRequest(" USER@EXAMPLE.COM ", password));

        Assert.Equal(ServiceResultStatus.Ok, result.Status);
        Assert.Equal("user@example.com", result.Value?.Email);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidEmail_ReturnsInvalid()
    {
        await using var db = CreateDb();
        var users = new EfRepository<AppUser>(db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters-long"
            })
            .Build();
        var tokens = TestServices.JwtTokenService(db, config);
        var service = new AuthenticationService(users, TestServices.Repo<RefreshToken>(db), tokens, TestServices.Uow(db));

        var result = await service.LoginAsync(new LoginRequest("unknown@example.com", "any-password"));

        Assert.Equal(ServiceResultStatus.Invalid, result.Status);
        Assert.Contains("E-mail ou senha invalidos", result.Error);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsInvalid()
    {
        await using var db = CreateDb();
        var users = new EfRepository<AppUser>(db);
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password")
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "test-jwt-key-with-more-than-thirty-two-characters-long"
            })
            .Build();
        var tokens = TestServices.JwtTokenService(db, config);
        var service = new AuthenticationService(users, TestServices.Repo<RefreshToken>(db), tokens, TestServices.Uow(db));

        var result = await service.LoginAsync(new LoginRequest("user@example.com", "wrong-password"));

        Assert.Equal(ServiceResultStatus.Invalid, result.Status);
        Assert.Contains("E-mail ou senha invalidos", result.Error);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
