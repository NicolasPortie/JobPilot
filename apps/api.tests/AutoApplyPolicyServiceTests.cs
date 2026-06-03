using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class AutoApplyPolicyServiceTests
{
    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    [Fact]
    public async Task EvaluateAsync_Allows_WhenScoreAboveThreshold()
    {
        var db = CreateDb();
        var service = new AutoApplyPolicyService(db);
        var userId = Guid.NewGuid();
        var lead = new JobLead { Id = Guid.NewGuid(), UserId = userId, Title = "Dev .NET", Score = 60 };
        var filter = new JobFilter { MinScore = 30, AutoIgnoreLowScore = false };

        var result = await service.EvaluateAsync(userId, lead, null, filter);

        Assert.True(result.CanApply);
    }

    [Fact]
    public async Task EvaluateAsync_Blocks_WhenScoreBelowThreshold()
    {
        var db = CreateDb();
        var service = new AutoApplyPolicyService(db);
        var userId = Guid.NewGuid();
        var lead = new JobLead { Id = Guid.NewGuid(), UserId = userId, Title = "Dev .NET", Score = 20 };
        var filter = new JobFilter { MinScore = 30, AutoIgnoreLowScore = false };

        var result = await service.EvaluateAsync(userId, lead, null, filter);

        Assert.False(result.CanApply);
        Assert.Contains("Score", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_Blocks_WhenUnknownMustHaveExists()
    {
        var db = CreateDb();
        var service = new AutoApplyPolicyService(db);
        var userId = Guid.NewGuid();
        var lead = new JobLead { Id = Guid.NewGuid(), UserId = userId, Title = "Dev .NET", Score = 60 };
        var filter = new JobFilter { MinScore = 30, AutoIgnoreLowScore = false };

        var target = new JobTarget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobLeadId = lead.Id,
            Title = lead.Title,
            Status = "organized",
            Requirements =
            [
                new JobRequirement { Id = Guid.NewGuid(), UserId = userId, Category = "tech", Label = "Kubernetes", Importance = "must_have", EvidenceText = "unknown", SortOrder = 1 }
            ]
        };
        db.JobTargets.Add(target);
        await db.SaveChangesAsync();

        var result = await service.EvaluateAsync(userId, lead, null, filter);

        Assert.False(result.CanApply);
        Assert.Contains("unknown", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_Allows_WhenNoUnknownMustHave()
    {
        var db = CreateDb();
        var service = new AutoApplyPolicyService(db);
        var userId = Guid.NewGuid();
        var lead = new JobLead { Id = Guid.NewGuid(), UserId = userId, Title = "Dev .NET", Score = 60 };
        var filter = new JobFilter { MinScore = 30, AutoIgnoreLowScore = false };

        var target = new JobTarget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobLeadId = lead.Id,
            Title = lead.Title,
            Status = "organized",
            Requirements =
            [
                new JobRequirement { Id = Guid.NewGuid(), UserId = userId, Category = "tech", Label = "C#", Importance = "must_have", EvidenceText = "proven", SortOrder = 1 }
            ]
        };
        db.JobTargets.Add(target);
        await db.SaveChangesAsync();

        var result = await service.EvaluateAsync(userId, lead, null, filter);

        Assert.True(result.CanApply);
    }
}
