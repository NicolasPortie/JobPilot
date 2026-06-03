using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class OperationalHealthServiceTests
{
    [Fact]
    public async Task GetAsync_ReportsConnectedExtensionAndCleanCommandQueue()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            Model = "qwen3:4b-instruct",
            IsActive = true,
            IsDefault = true
        });
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = "connected",
            ExtensionVersion = "0.1.4",
            IsSupportedPage = true,
            CurrentUrl = "https://www.linkedin.com/jobs/view/123/",
            LastHeartbeatAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("ok", result.Status);
        Assert.Contains(result.Signals, signal => signal.Key == "executor" && signal.Status == "ok");
        Assert.Contains(result.Signals, signal => signal.Key == "commands" && signal.Status == "ok");
    }

    [Fact]
    public async Task GetAsync_WarnsWhenExtensionIsOutdatedAndCommandIsStale()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = sessionId,
            UserId = userId,
            Status = "connected",
            ExtensionVersion = "0.1.0",
            LastHeartbeatAt = DateTimeOffset.UtcNow
        });
        db.BrowserExtensionCommandJobs.Add(new BrowserExtensionCommandJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BrowserExtensionSessionId = sessionId,
            Type = "observe",
            Status = "claimed",
            ClaimedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("attention", result.Status);
        Assert.Equal(1, result.Metrics.StaleExtensionCommands);
        Assert.Contains(result.Signals, signal => signal.Key == "executor" && signal.Status == "warning");
        Assert.Contains(result.Signals, signal => signal.Key == "commands" && signal.Status == "warning");
    }

    [Fact]
    public async Task GetAsync_DoesNotWarnForMissingExtensionWhenLocalDedicatedBrowserIsSelected()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser
        });
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            Model = "qwen3:4b-instruct",
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("ok", result.Status);
        Assert.Contains(result.Signals, signal => signal.Key == "executor" && signal.Status == "ok" && signal.Label.Contains("Navegador local"));
        Assert.Contains(result.Signals, signal => signal.Key == "commands" && signal.Status == "ok" && signal.Label.Contains("inativa"));
        Assert.DoesNotContain(result.Signals, signal => signal.Label.Contains("Extensao nao pareada"));
    }

    [Fact]
    public async Task GetAsync_ReportsAttentionWhenPolicyIsPaused()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser,
            IsPaused = true
        });
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            Model = "qwen3:4b-instruct",
            IsActive = true,
            IsDefault = true
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("attention", result.Status);
        Assert.Contains(result.Signals, signal => signal.Key == "policy" && signal.Status == "warning" && signal.Label.Contains("pausada"));
    }

    [Fact]
    public async Task GetAsync_WarnsWhenThereIsStaleWorkForMaintenance()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var leadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser
        });
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            Model = "qwen3:4b-instruct",
            IsActive = true,
            IsDefault = true
        });
        db.AgentRuns.Add(new AgentRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgentName = "ApplicationAgent",
            Mode = "application",
            State = "running",
            StartedAt = now.AddHours(-3)
        });
        db.JobLeads.Add(new JobLead
        {
            Id = leadId,
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Url = "https://www.linkedin.com/jobs/view/1",
            Status = JobStatuses.Filling
        });
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = jobId,
            UserId = userId,
            JobLeadId = leadId,
            JobUrl = "https://www.linkedin.com/jobs/view/1",
            State = JobStatuses.Filling,
            CreatedAt = now.AddMinutes(-30)
        });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = jobId,
            EventType = "agent.step",
            Detail = "old activity",
            OccurredAt = now.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("attention", result.Status);
        Assert.Equal(1, result.Metrics.StaleAgentRuns);
        Assert.Equal(1, result.Metrics.StaleApplicationJobs);
        Assert.Contains(result.Signals, signal => signal.Key == "maintenance" && signal.Status == "warning");
    }

    [Fact]
    public async Task GetAsync_WarnsWhenDailyLimitIsReached()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var applicationJobId = Guid.NewGuid();
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser,
            MaxApplicationsPerDay = 1
        });
        db.AiProviders.Add(new AiProvider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Ollama Local",
            ProviderType = "ollama",
            Model = "qwen3:4b-instruct",
            IsActive = true,
            IsDefault = true
        });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = applicationJobId,
            EventType = "job.applied",
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(userId);

        Assert.Equal("attention", result.Status);
        Assert.Equal(1, result.Metrics.DailyLimit);
        Assert.Equal(1, result.Metrics.AppliedToday);
        Assert.Equal(0, result.Metrics.RemainingToday);
        Assert.Contains(result.Signals, signal => signal.Key == "limits" && signal.Status == "warning" && signal.Label.Contains("atingido"));
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static OperationalHealthService CreateService(JobPilotDbContext db) =>
        new(db, new ApplicationLimitService(db));
}
