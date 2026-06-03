using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class BrowserExtensionSessionTests
{
    [Fact]
    public async Task BrowserExtensionSession_CanBeResolvedByInstallationIdForIdempotentLocalPairing()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Chrome local",
            InstallationId = "install-123",
            TokenHash = "old-token-hash",
            Status = "paired",
            Browser = "chrome",
            ExtensionVersion = "0.1.0"
        });
        await db.SaveChangesAsync();

        var existing = await db.BrowserExtensionSessions.SingleOrDefaultAsync(x =>
            x.UserId == userId &&
            x.InstallationId == "install-123" &&
            x.Status != "revoked");

        Assert.NotNull(existing);
        existing!.TokenHash = "new-token-hash";
        await db.SaveChangesAsync();

        Assert.Single(await db.BrowserExtensionSessions.Where(x => x.UserId == userId).ToListAsync());
        Assert.Equal("new-token-hash", (await db.BrowserExtensionSessions.SingleAsync()).TokenHash);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
