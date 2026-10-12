using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobPilot.Api.Tests;

public sealed class PlatformCredentialServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsCredentialsForUser()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        db.PlatformCredentials.Add(new PlatformCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "linkedin",
            AccountEmail = "user@example.com",
            EncryptedPassword = "",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await service.ListAsync(userId);

        Assert.Single(result);
        Assert.Equal("linkedin", result[0].Platform);
    }

    [Fact]
    public async Task CreateAsync_CreatesCredential()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.CreateAsync(userId, new PlatformCredentialRequest("indeed", "indeed@example.com", "secret123", true));

        Assert.Equal(ServiceResultStatus.Created, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("indeed", result.Value.Platform);
        Assert.True(result.Value.HasPassword);
        Assert.Equal(1, await db.PlatformCredentials.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCredential()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        db.PlatformCredentials.Add(new PlatformCredential
        {
            Id = id,
            UserId = userId,
            Platform = "linkedin",
            AccountEmail = "old@example.com",
            EncryptedPassword = "",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await service.UpdateAsync(userId, id, new PlatformCredentialRequest("linkedin", "new@example.com", "newpass", false));

        Assert.Equal(ServiceResultStatus.Ok, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("new@example.com", result.Value.AccountEmail);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WithMissingCredential_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new PlatformCredentialRequest("x", "x@x.com", "pass", true));

        Assert.Equal(ServiceResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCredential()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        db.PlatformCredentials.Add(new PlatformCredential
        {
            Id = id,
            UserId = userId,
            Platform = "linkedin",
            AccountEmail = "user@example.com",
            EncryptedPassword = "",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(userId, id);

        Assert.Equal(ServiceResultStatus.NoContent, result.Status);
        Assert.Equal(0, await db.PlatformCredentials.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_WithMissingCredential_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ServiceResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListForAgentAsync_ReturnsOnlyActiveCredentialsWithDecryptedPasswords()
    {
        await using var db = CreateDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters"
            })
            .Build();
        var credentials = new EfRepository<PlatformCredential>(db);
        var unitOfWork = new EfUnitOfWork(db);
        var service = new PlatformCredentialService(credentials, unitOfWork, config, NullLogger<PlatformCredentialService>.Instance);
        var userId = Guid.NewGuid();
        db.PlatformCredentials.AddRange(
            new PlatformCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = "linkedin",
                AccountEmail = "user@example.com",
                EncryptedPassword = SecretProtector.Protect("linkedin-pass", config),
                IsActive = true
            },
            new PlatformCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = "indeed",
                AccountEmail = "user@example.com",
                EncryptedPassword = SecretProtector.Protect("indeed-pass", config),
                IsActive = false
            }
        );
        await db.SaveChangesAsync();

        var result = await service.ListForAgentAsync(userId);

        Assert.Single(result);
        Assert.Equal("linkedin", result[0].Platform);
        Assert.Equal("linkedin-pass", result[0].Password);
    }

    [Fact]
    public async Task ListForAgentAsync_WhenPasswordEncryptedWithLegacyJwtKey_MigratesAndReturnsCredential()
    {
        var jwtConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "legacy-jwt-key-with-more-than-32-characters",
                ["Security:AllowLegacyJwtDataProtection"] = "true"
            })
            .Build();
        var currentConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtKey"] = "legacy-jwt-key-with-more-than-32-characters",
                ["Security:SecretKey"] = "current-secret-key-with-more-than-32-chars",
                ["Security:AllowLegacyJwtDataProtection"] = "true"
            })
            .Build();

        await using var db = CreateDb();
        var credentials = new EfRepository<PlatformCredential>(db);
        var unitOfWork = new EfUnitOfWork(db);
        var service = new PlatformCredentialService(credentials, unitOfWork, currentConfig, NullLogger<PlatformCredentialService>.Instance);
        var userId = Guid.NewGuid();
        db.PlatformCredentials.Add(new PlatformCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Platform = "linkedin",
            AccountEmail = "user@example.com",
            EncryptedPassword = SecretProtector.Protect("linkedin-pass", jwtConfig),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await service.ListForAgentAsync(userId);

        Assert.Single(result);
        Assert.Equal("linkedin-pass", result[0].Password);
        var stored = await db.PlatformCredentials.SingleAsync();
        Assert.Equal("linkedin-pass", SecretProtector.Unprotect(stored.EncryptedPassword, currentConfig));
    }

    [Fact]
    public async Task ListForAgentAsync_WhenPasswordEncryptedWithDifferentKey_SkipsInsteadOfThrowing()
    {
        // Reproduz a causa-raiz do BUG 1: a senha foi cifrada com uma chave antiga.
        // Antes, Unprotect lançava CryptographicException -> 500 -> agente caía em variáveis de ambiente.
        await using var db = CreateDb();
        var oldKeyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretKey"] = "OLD-secret-key-with-more-than-thirty-two-characters"
            })
            .Build();
        var currentConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretKey"] = "NEW-secret-key-with-more-than-thirty-two-characters"
            })
            .Build();

        var credentials = new EfRepository<PlatformCredential>(db);
        var unitOfWork = new EfUnitOfWork(db);
        var service = new PlatformCredentialService(credentials, unitOfWork, currentConfig, NullLogger<PlatformCredentialService>.Instance);
        var userId = Guid.NewGuid();
        db.PlatformCredentials.AddRange(
            new PlatformCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = "linkedin",
                AccountEmail = "stale@example.com",
                EncryptedPassword = SecretProtector.Protect("stale-pass", oldKeyConfig),
                IsActive = true
            },
            new PlatformCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = "indeed",
                AccountEmail = "fresh@example.com",
                EncryptedPassword = SecretProtector.Protect("fresh-pass", currentConfig),
                IsActive = true
            }
        );
        await db.SaveChangesAsync();

        var result = await service.ListForAgentAsync(userId);

        // Não lança; retorna apenas a credencial decriptável.
        Assert.Single(result);
        Assert.Equal("indeed", result[0].Platform);
        Assert.Equal("fresh-pass", result[0].Password);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static PlatformCredentialService CreateService(JobPilotDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SecretKey"] = "test-secret-key-with-more-than-thirty-two-characters"
            })
            .Build();
        var credentials = new EfRepository<PlatformCredential>(db);
        var unitOfWork = new EfUnitOfWork(db);
        return new PlatformCredentialService(credentials, unitOfWork, config, NullLogger<PlatformCredentialService>.Instance);
    }
}
