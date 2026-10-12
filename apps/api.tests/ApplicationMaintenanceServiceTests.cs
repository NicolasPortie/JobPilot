using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ApplicationMaintenanceServiceTests
{
    [Fact]
    public async Task RecoverStaleWorkAsync_MarcaRunAntigoComoFailed()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.AgentRuns.Add(new AgentRun
        {
            Id = runId,
            UserId = userId,
            AgentName = "ApplicationAgent",
            Mode = "application",
            State = "running",
            StartedAt = now.AddHours(-3)
        });
        await db.SaveChangesAsync();

        var result = await TestServices.ApplicationMaintenanceService(db).RecoverStaleWorkAsync(now);

        var run = await db.AgentRuns.SingleAsync(x => x.Id == runId);
        Assert.Equal(1, result.StaleAgentRuns);
        Assert.Equal("failed", run.State);
        Assert.Equal(now, run.CompletedAt);
        Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "maintenance.agent_run_stale");
    }

    [Fact]
    public async Task RecoverStaleWorkAsync_NaoMarcaRunRecente()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.AgentRuns.Add(new AgentRun
        {
            Id = runId,
            UserId = userId,
            AgentName = "ApplicationAgent",
            Mode = "application",
            State = "running",
            StartedAt = now.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var result = await TestServices.ApplicationMaintenanceService(db).RecoverStaleWorkAsync(now);

        var run = await db.AgentRuns.SingleAsync(x => x.Id == runId);
        Assert.Equal(0, result.StaleAgentRuns);
        Assert.Equal("running", run.State);
        Assert.Null(run.CompletedAt);
    }

    [Fact]
    public async Task RecoverStaleWorkAsync_ComUsuarioNaoAlteraTrabalhoDeOutroUsuario()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownRunId = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.AgentRuns.AddRange(
            new AgentRun
            {
                Id = ownRunId,
                UserId = userId,
                AgentName = "ApplicationAgent",
                Mode = "application",
                State = "running",
                StartedAt = now.AddHours(-3)
            },
            new AgentRun
            {
                Id = otherRunId,
                UserId = otherUserId,
                AgentName = "ApplicationAgent",
                Mode = "application",
                State = "running",
                StartedAt = now.AddHours(-3)
            });
        await db.SaveChangesAsync();

        var result = await TestServices.ApplicationMaintenanceService(db).RecoverStaleWorkAsync(now, userId);

        Assert.Equal(1, result.StaleAgentRuns);
        Assert.Equal("failed", (await db.AgentRuns.SingleAsync(x => x.Id == ownRunId)).State);
        Assert.Equal("running", (await db.AgentRuns.SingleAsync(x => x.Id == otherRunId)).State);
    }

    [Fact]
    public async Task RecoverStaleWorkAsync_MoveTarefaPresaParaRevisao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
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
            Detail = "last activity",
            OccurredAt = now.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var result = await TestServices.ApplicationMaintenanceService(db).RecoverStaleWorkAsync(now);

        var job = await db.ApplicationJobs.Include(x => x.JobLead).SingleAsync(x => x.Id == jobId);
        Assert.Equal(1, result.StaleApplicationJobs);
        Assert.Equal(JobStatuses.NeedsReview, job.State);
        Assert.Equal("agent_interrupted", job.Reason);
        Assert.Equal(JobStatuses.NeedsReview, job.JobLead!.Status);
        Assert.Equal("review_required", job.JobLead.AutomationSupport);
        Assert.Contains(await db.ExecutionEvents.ToListAsync(), x => x.EventType == "maintenance.application_job_stale");
    }

    [Fact]
    public async Task RecoverStaleWorkAsync_PreservaTarefaComAtividadeRecente()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.JobLeads.Add(new JobLead
        {
            Id = leadId,
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Url = "https://www.linkedin.com/jobs/view/2",
            Status = JobStatuses.Opening
        });
        db.ApplicationJobs.Add(new ApplicationJob
        {
            Id = jobId,
            UserId = userId,
            JobLeadId = leadId,
            JobUrl = "https://www.linkedin.com/jobs/view/2",
            State = JobStatuses.Opening,
            CreatedAt = now.AddMinutes(-30)
        });
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = jobId,
            EventType = "agent.step",
            Detail = "recent activity",
            OccurredAt = now.AddMinutes(-2)
        });
        await db.SaveChangesAsync();

        var result = await TestServices.ApplicationMaintenanceService(db).RecoverStaleWorkAsync(now);

        var job = await db.ApplicationJobs.SingleAsync(x => x.Id == jobId);
        Assert.Equal(0, result.StaleApplicationJobs);
        Assert.Equal(JobStatuses.Opening, job.State);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
