using JobPilot.Api.Application;
using JobPilot.Api.Application.Contracts;
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

    [Fact]
    public async Task SaveAsync_NormalizesStateAndSkills()
    {
        await using var db = CreateDb();
        var service = TestServices.CandidateProfileService(db);

        var response = await service.SaveAsync(Guid.NewGuid(), Request("Ana") with { State = " s1p ", Skills = [" React ", "react", "", "Vue"] });

        Assert.Equal("SP", response.State);
        Assert.Equal(["React", "Vue"], response.Skills);
    }

    [Fact]
    public async Task AddSkillAsync_AddsOnce_SortsAndRecordsRecommendation()
    {
        await using var db = CreateDb();
        var service = TestServices.CandidateProfileService(db);
        var userId = Guid.NewGuid();
        await service.SaveAsync(userId, Request("Ana"));

        var first = await service.AddSkillAsync(userId, new AddProfileSkillRequest("  Docker ", "missing", null, "Dev", "ACME"));
        var again = await service.AddSkillAsync(userId, new AddProfileSkillRequest("docker", "missing", null, "Dev", "ACME"));

        Assert.False(first.AlreadyPresent);
        Assert.True(again.AlreadyPresent);
        Assert.Equal([".NET", "C#", "Docker"], first.Profile.Skills);
        var history = await service.ListSkillRecommendationsAsync(userId);
        var item = Assert.Single(history);
        Assert.Equal("Docker", item.Skill);
        Assert.Equal("ACME", item.Company);
    }

    [Fact]
    public async Task SkillRecommendations_AreIsolatedPerUser_AndRemovable()
    {
        await using var db = CreateDb();
        var service = TestServices.CandidateProfileService(db);
        var owner = Guid.NewGuid();
        await service.AddSkillAsync(owner, new AddProfileSkillRequest("Kubernetes", "related", "Veio da vaga", null, null));
        var id = Assert.Single(await service.ListSkillRecommendationsAsync(owner)).Id;

        Assert.Empty(await service.ListSkillRecommendationsAsync(Guid.NewGuid()));
        Assert.False(await service.RemoveSkillRecommendationAsync(Guid.NewGuid(), id));
        Assert.True(await service.RemoveSkillRecommendationAsync(owner, id));
        Assert.Empty(await service.ListSkillRecommendationsAsync(owner));
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
