using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public sealed class CandidateProfileServiceTests
{
    [Fact]
    public async Task SaveAsync_UsesAuthenticatedUserAndReturnsPublicContract()
    {
        await using var db = CreateDb();
        var service = TestServices.CandidateProfileService(db);
        var userId = Guid.NewGuid();

        var response = await service.SaveAsync(userId, Request("Nicolas Portie"));

        Assert.Equal("Nicolas Portie", response.FullName);
        Assert.Equal(userId, (await db.CandidateProfiles.SingleAsync()).UserId);
    }

    [Fact]
    public async Task GetAsync_DoesNotReturnAnotherUsersProfile()
    {
        await using var db = CreateDb();
        var service = TestServices.CandidateProfileService(db);
        await service.SaveAsync(Guid.NewGuid(), Request("Another User"));

        var response = await service.GetAsync(Guid.NewGuid());

        Assert.Null(response.Id);
        Assert.Equal("", response.FullName);
    }

    private static CandidateProfileRequest Request(string name) =>
        new(name, "user@example.com", "11999999999", "Sao Paulo", "SP", "Brazil", "", "", "", "", ["C#", ".NET"], ".NET Developer", "junior", "");

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
