using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class AutomationProcessServiceTests
{
    [Fact]
    public async Task StartApplyAsync_DefaultExtensionDriverRequiresConnectedExtension()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId });
        await db.SaveChangesAsync();
        var previous = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(true, 1));

            Assert.Contains("BadRequest", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            Assert.Contains("extensao", System.Text.Json.JsonSerializer.Serialize(value), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previous);
        }
    }

    [Fact]
    public async Task StartDiscoverAsync_NaoIniciaQuandoPoliticaEstaPausada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser,
            IsPaused = true
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartDiscoverAsync(userId, new AutomationDiscoverRequest(".NET", "Brazil", 10));

            Assert.Contains("Ok", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains("paused", json);
            Assert.Contains("automation.policy.paused", (await db.ExecutionEvents.SingleAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
        }
    }

    [Fact]
    public async Task StartApplyAsync_NaoIniciaQuandoPoliticaEstaPausada()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser,
            IsPaused = true
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 10));

            Assert.Contains("Ok", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains("paused", json);
            Assert.Contains("automation.policy.paused", (await db.ExecutionEvents.SingleAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
        }
    }

    [Fact]
    public async Task StartApplyAsync_DefaultExtensionDriverRejectsOutdatedExtension()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId });
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Chrome local",
            Status = "connected",
            Browser = "chrome",
            ExtensionVersion = "0.1.0",
            LastHeartbeatAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var previous = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(true, 1));

            Assert.Contains("BadRequest", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains("0.1.4", json);
            Assert.Contains("Recarregue", json);
            Assert.Contains("automation.extension.outdated", (await db.ExecutionEvents.SingleAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previous);
        }
    }

    [Fact]
    public async Task StartApplyAsync_RecoversStaleOpeningJobs()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var applicationJobId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId });
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Chrome local",
            Status = "connected",
            Browser = "chrome",
            ExtensionVersion = "0.1.4",
            LastHeartbeatAt = DateTimeOffset.UtcNow
        });
        db.JobLeads.Add(new JobLead
        {
            Id = leadId,
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Url = "https://www.linkedin.com/jobs/view/1/",
            Status = JobStatuses.Opening
        });
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = applicationJobId,
            UserId = userId,
            JobLeadId = leadId,
            JobUrl = "https://www.linkedin.com/jobs/view/1/",
            State = JobStatuses.Opening,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = applicationJobId,
            EventType = "extension.lead_opened",
            Detail = "url=https://www.linkedin.com/jobs/view/1/",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await db.SaveChangesAsync();
        var previous = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 1));

            Assert.Contains("Ok", result.GetType().Name);
            var job = await db.ApplicationJobs.Include(x => x.JobLead).SingleAsync(x => x.Id == applicationJobId);
            Assert.Equal(JobStatuses.NeedsReview, job.State);
            Assert.Equal("agent_interrupted", job.Reason);
            Assert.Equal(JobStatuses.NeedsReview, job.JobLead!.Status);
            Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "automation.apply.recovered_stale_opening");
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previous);
        }
    }

    [Fact]
    public async Task StartApplyAsync_DoesNotRequeueReadyForSubmitJobs()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy { Id = Guid.NewGuid(), UserId = userId });
        db.BrowserExtensionSessions.Add(new BrowserExtensionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Chrome local",
            Status = "connected",
            Browser = "chrome",
            ExtensionVersion = "0.1.4",
            LastHeartbeatAt = DateTimeOffset.UtcNow
        });
        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "User Test",
            Email = "user@test.local",
            Phone = "(18) 99999-9999",
            TargetRole = ".NET",
            Seniority = "junior",
            Skills = [".NET", "C#"]
        });
        db.JobLeads.Add(new JobLead
        {
            Id = leadId,
            UserId = userId,
            Title = "Desenvolvedor .NET Junior",
            Url = "https://www.linkedin.com/jobs/view/2/",
            Status = JobStatuses.ReadyForSubmit,
            Score = 90,
            IsRemote = true
        });
        await db.SaveChangesAsync();
        var previous = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 1));

            Assert.Contains("Ok", result.GetType().Name);
            Assert.False(await db.ApplicationJobs.AnyAsync());
            Assert.Equal(JobStatuses.ReadyForSubmit, (await db.JobLeads.SingleAsync(x => x.Id == leadId)).Status);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previous);
        }
    }

    [Fact]
    public async Task StartApplyAsync_LocalDedicatedBrowserTargetDoesNotRequireExtension()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        var previousDriver = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 1));

            Assert.Contains("Ok", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains(ExecutionTargets.LocalDedicatedBrowser, json);
            Assert.Contains("playwright", json);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previousDriver);
        }
    }

    [Fact]
    public async Task StartApplyAsync_NaoIniciaQuandoLimiteDiarioAtingido()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser,
            MaxApplicationsPerDay = 1
        });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = Guid.NewGuid(),
            EventType = "job.applied",
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        var previousDriver = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 10));

            Assert.Contains("Ok", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains("dailyLimitReached", json);
            Assert.Contains("automation.apply.daily_limit_reached", (await db.ExecutionEvents.OrderBy(x => x.OccurredAt).LastAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previousDriver);
        }
    }

    [Fact]
    public async Task StartApplyAsync_NaoIniciaOutroPilotoQuandoRunApplicationEstaAtivo()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser
        });
        db.AgentRuns.Add(new AgentRun
        {
            Id = runId,
            UserId = userId,
            AgentName = "ApplicationAgent",
            Mode = "application",
            State = "running",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        var previousDriver = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 10));

            Assert.Contains("Ok", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains("alreadyRunning", json);
            Assert.Contains(runId.ToString(), json);
            Assert.Contains("automation.apply.already_running", (await db.ExecutionEvents.SingleAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previousDriver);
        }
    }

    [Fact]
    public async Task StartApplyAsync_IgnoraRunApplicationStaleAntesDeIniciar()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.LocalDedicatedBrowser
        });
        db.AgentRuns.Add(new AgentRun
        {
            Id = runId,
            UserId = userId,
            AgentName = "ApplicationAgent",
            Mode = "application",
            State = "running",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-3)
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        var previousDriver = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 1));

            Assert.DoesNotContain("already_running", string.Join(";", await db.ExecutionEvents.Select(x => x.EventType).ToListAsync()));
            var staleRun = await db.AgentRuns.SingleAsync(x => x.Id == runId);
            Assert.Equal("failed", staleRun.State);
            Assert.NotNull(staleRun.CompletedAt);
            Assert.Contains("Ok", result.GetType().Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previousDriver);
        }
    }

    [Fact]
    public async Task StartApplyAsync_RemoteBrowserTargetReturnsUnsupportedMessage()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, Email = "user@test.local", PasswordHash = "hash" });
        db.AgentPolicies.Add(new AgentPolicy
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExecutionTarget = ExecutionTargets.RemoteBrowser
        });
        await db.SaveChangesAsync();
        var previousTarget = Environment.GetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET");
        var previousDriver = Environment.GetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER");
        Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", null);
        Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", null);
        try
        {
            var service = CreateService(db);

            var result = await service.StartApplyAsync(userId, new AutomationApplyRequest(false, 1));

            Assert.Contains("BadRequest", result.GetType().Name);
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            Assert.Contains(ExecutionTargets.RemoteBrowser, json);
            Assert.Contains("worker remoto", json);
            Assert.Contains("automation.execution_target.unsupported", (await db.ExecutionEvents.SingleAsync()).EventType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_EXECUTION_TARGET", previousTarget);
            Environment.SetEnvironmentVariable("JOBPILOT_AUTOMATION_BROWSER_DRIVER", previousDriver);
        }
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static AutomationProcessService CreateService(JobPilotDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            })
            .Build();
        return new AutomationProcessService(db, new JwtTokenService(config), new JobFilterService(db), new ScoringService(), new ApplicationLimitService(db), new AutoApplyPolicyService(db));
    }
}
