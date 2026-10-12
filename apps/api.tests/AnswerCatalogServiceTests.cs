using JobPilot.Api.Application;
using JobPilot.Api.Data;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public sealed class AnswerCatalogServiceTests
{
    [Fact]
    public async Task CreateAnswerAsync_UsesAuthenticatedUserAndPublicContract()
    {
        await using var db = CreateDb();
        var service = TestServices.AnswerCatalogService(db);
        var userId = Guid.NewGuid();

        var result = await service.CreateAnswerAsync(
            userId,
            new ReusableAnswerRequest("salary", "Salary?", "5000", "compensation"));

        Assert.Equal(ServiceResultStatus.Created, result.Status);
        Assert.Equal("salary", result.Value?.QuestionKey);
        Assert.Equal(userId, (await db.ReusableAnswers.SingleAsync()).UserId);
    }

    [Fact]
    public async Task UpdateAnswerAsync_DoesNotModifyAnotherUsersAnswer()
    {
        await using var db = CreateDb();
        var service = TestServices.AnswerCatalogService(db);
        var ownerId = Guid.NewGuid();
        var created = await service.CreateAnswerAsync(
            ownerId,
            new ReusableAnswerRequest("notice", "Notice?", "30 days", "general"));

        var result = await service.UpdateAnswerAsync(
            Guid.NewGuid(),
            created.Value!.Id,
            new ReusableAnswerRequest("notice", "Notice?", "Immediate", "general"));

        Assert.Equal(ServiceResultStatus.NotFound, result.Status);
        Assert.Equal("30 days", (await db.ReusableAnswers.SingleAsync()).AnswerValue);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
