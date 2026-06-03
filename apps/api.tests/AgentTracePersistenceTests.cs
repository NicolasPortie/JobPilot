using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class AgentTracePersistenceTests
{
    [Fact]
    public async Task ApplicationAuditService_ReturnsRichTraceStepsInStepOrder()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var applicationJobId = SeedApplicationJob(db, userId);
        var runId = Guid.NewGuid();
        db.AgentTraceSteps.AddRange(
            Trace(userId, applicationJobId, runId, 2, "second"),
            Trace(userId, applicationJobId, runId, 1, "first"));
        await db.SaveChangesAsync();
        var audit = new ApplicationAuditService(db);

        var steps = await audit.GetTraceStepsAsync(userId, applicationJobId);

        Assert.Collection(
            steps,
            first =>
            {
                Assert.Equal(1, first.StepNumber);
                Assert.Contains("first", first.ProposalJson);
                Assert.Equal(runId, first.AgentRunId);
            },
            second => Assert.Equal(2, second.StepNumber));
    }

    [Fact]
    public async Task AgentTraceStep_PersistsObjectiveRiskVerdictOutcomeAndSafetyJson()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var applicationJobId = SeedApplicationJob(db, userId);
        var runId = Guid.NewGuid();
        var trace = Trace(userId, applicationJobId, runId, 1, "submit");
        trace.RiskJson = """{"level":"High","reasons":["submit_boundary"]}""";
        trace.GuardVerdictJson = """{"allowed":false,"verdict":"risk_requires_checkpoint"}""";
        trace.OutcomeJson = """{"status":"blocked_by_guard"}""";
        trace.ContentSafetyJson = """{"hasSignals":true,"signals":[{"code":"prompt_injection"}]}""";
        db.AgentTraceSteps.Add(trace);
        await db.SaveChangesAsync();

        var saved = await db.AgentTraceSteps.SingleAsync(x => x.UserId == userId && x.ApplicationJobId == applicationJobId);

        Assert.Contains("submit_boundary", saved.RiskJson);
        Assert.Contains("risk_requires_checkpoint", saved.GuardVerdictJson);
        Assert.Contains("blocked_by_guard", saved.OutcomeJson);
        Assert.Contains("prompt_injection", saved.ContentSafetyJson);
    }

    [Fact]
    public void TraceRedactionService_RedactsSecretsAndCpfInTracePayloads()
    {
        var redactor = new TraceRedactionService();

        var redacted = redactor.Redact("""{"password":"hunter2","authorization":"Bearer abcdefghijklmnop","cpf":"12345678910"}""");

        Assert.DoesNotContain("hunter2", redacted);
        Assert.DoesNotContain("abcdefghijklmnop", redacted);
        Assert.DoesNotContain("12345678910", redacted);
        Assert.Contains("[redacted", redacted);
    }

    [Fact]
    public async Task CheckpointResolution_KeepsResumeMetadataAndCanPersistReusableAnswer()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var applicationJobId = SeedApplicationJob(db, userId);
        var runId = Guid.NewGuid();
        var traceId = Guid.NewGuid();
        var checkpoint = new Checkpoint
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = applicationJobId,
            AgentRunId = runId,
            TraceStepId = traceId,
            ReasonCode = "answer_needs_confirmation",
            Stage = "runtime",
            FieldLabel = "Pretensao salarial",
            Detail = "Confirm answer",
            SuggestedUserAction = "Confirm or edit the drafted answer.",
            ResumeMode = "reobserve",
            ResolutionStatus = "open"
        };
        db.Checkpoints.Add(checkpoint);
        await db.SaveChangesAsync();
        var service = new CheckpointResolutionService(db, new ReusableAnswerService(db));

        var resolved = await service.ResolveAsync(userId, checkpoint.Id, new ResolveCheckpointRequest("resolved", "R$ 5000"));

        Assert.NotNull(resolved);
        Assert.Equal("resolved", resolved!.ResolutionStatus);
        Assert.Equal(runId, resolved.AgentRunId);
        Assert.Equal(traceId, resolved.TraceStepId);
        Assert.Equal("reobserve", resolved.ResumeMode);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Contains(await db.ReusableAnswers.ToListAsync(), answer =>
            answer.UserId == userId &&
            answer.QuestionText == "Pretensao salarial" &&
            answer.AnswerValue == "R$ 5000");
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static Guid SeedApplicationJob(JobPilotDbContext db, Guid userId)
    {
        var leadId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var applicationJobId = Guid.NewGuid();
        db.JobLeads.Add(new JobLead
        {
            Id = leadId,
            UserId = userId,
            Title = "Developer",
            Company = "Example",
            Url = "https://jobs.example.test/1"
        });
        db.ApplicationRuns.Add(new ApplicationRun
        {
            Id = runId,
            UserId = userId,
            Name = "test",
            TotalJobs = 1,
            Jobs =
            [
                new ApplicationJob
                {
                    Id = applicationJobId,
                    UserId = userId,
                    ApplicationRunId = runId,
                    JobLeadId = leadId,
                    JobUrl = "https://jobs.example.test/1",
                    State = JobStatuses.QueuedToApply
                }
            ]
        });
        db.SaveChanges();
        return applicationJobId;
    }

    private static AgentTraceStep Trace(Guid userId, Guid applicationJobId, Guid runId, int stepNumber, string label) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgentRunId = runId,
            ApplicationJobId = applicationJobId,
            StepNumber = stepNumber,
            ObjectiveKind = "apply",
            ObjectiveJson = """{"kind":"apply"}""",
            Platform = "Generic",
            Module = "GenericPlatformModule",
            Phase = "form",
            BeforeFingerprint = $"before-{stepNumber}",
            AfterFingerprint = $"after-{stepNumber}",
            Planner = "local",
            ProposalJson = $$"""{"type":"click","label":"{{label}}"}""",
            RiskJson = """{"level":"Low"}""",
            GuardVerdictJson = """{"allowed":true,"verdict":"approved"}""",
            ExecutionJson = """{"success":true}""",
            OutcomeJson = """{"status":"validated_changed"}""",
            RecoveryJson = """{"mode":"None"}""",
            ContentSafetyJson = """{"hasSignals":false}""",
            EvidencePath = $"evidence-{stepNumber}.png",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
