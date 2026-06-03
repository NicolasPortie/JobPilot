using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ApplicationResumeServiceTests
{
    [Fact]
    public async Task AssignAsync_VinculaVersaoEExportacaoAoApplicationJob()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var exportPath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(exportPath, "Curriculo ATS");

        db.JobLeads.Add(new JobLead { Id = leadId, UserId = userId, Title = "Dev .NET", Url = "https://example.test/job" });
        db.ApplicationRuns.Add(new ApplicationRun { Id = runId, UserId = userId, Name = "Run" });
        db.ApplicationJobs.Add(new ApplicationJob { Id = taskId, UserId = userId, ApplicationRunId = runId, JobLeadId = leadId, JobUrl = "https://example.test/job" });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Direcionado", PlainText = "Curriculo ATS" });
        db.ResumeExports.Add(new ResumeExport { Id = exportId, UserId = userId, ResumeVersionId = versionId, Format = "txt", FilePath = exportPath, ParseText = "Curriculo ATS" });
        await db.SaveChangesAsync();

        var service = new ApplicationResumeService(db);
        var result = await service.AssignAsync(userId, taskId, new ApplicationJobResumeRequest(versionId, exportId));

        Assert.Equal(taskId, result.ApplicationJobId);
        Assert.Equal(versionId, result.ResumeVersionId);
        Assert.Equal(exportId, result.ResumeExportId);
        Assert.Equal(exportPath, result.ResumeFilePath);

        var task = await db.ApplicationJobs.SingleAsync(x => x.Id == taskId);
        Assert.Equal(versionId, task.ResumeVersionId);
        Assert.Equal(exportId, task.ResumeExportId);
        Assert.Equal(exportPath, task.ResumeFilePath);
        Assert.Contains(await db.ExecutionEvents.Where(x => x.ApplicationJobId == taskId).ToListAsync(), x => x.EventType == "application.resume_assigned");
    }

    [Fact]
    public async Task AssignAsync_RejeitaExportacaoDeOutraVersao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var otherVersionId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var exportPath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(exportPath, "Curriculo ATS");

        db.JobLeads.Add(new JobLead { Id = leadId, UserId = userId, Title = "Dev .NET", Url = "https://example.test/job" });
        db.ApplicationRuns.Add(new ApplicationRun { Id = runId, UserId = userId, Name = "Run" });
        db.ApplicationJobs.Add(new ApplicationJob { Id = taskId, UserId = userId, ApplicationRunId = runId, JobLeadId = leadId, JobUrl = "https://example.test/job" });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Direcionado", PlainText = "Curriculo ATS" });
        db.ResumeVersions.Add(new ResumeVersion { Id = otherVersionId, UserId = userId, ResumeDocumentId = documentId, Title = "Outro", PlainText = "Outro" });
        db.ResumeExports.Add(new ResumeExport { Id = exportId, UserId = userId, ResumeVersionId = otherVersionId, Format = "txt", FilePath = exportPath, ParseText = "Outro" });
        await db.SaveChangesAsync();

        var service = new ApplicationResumeService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(userId, taskId, new ApplicationJobResumeRequest(versionId, exportId)));
    }

    [Fact]
    public async Task AssignAsync_RejeitaExportacaoDeOutroUsuario()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var exportPath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(exportPath, "Curriculo ATS");

        db.JobLeads.Add(new JobLead { Id = leadId, UserId = userId, Title = "Dev .NET", Url = "https://example.test/job" });
        db.ApplicationRuns.Add(new ApplicationRun { Id = runId, UserId = userId, Name = "Run" });
        db.ApplicationJobs.Add(new ApplicationJob { Id = taskId, UserId = userId, ApplicationRunId = runId, JobLeadId = leadId, JobUrl = "https://example.test/job" });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Direcionado", PlainText = "Curriculo ATS" });
        db.ResumeExports.Add(new ResumeExport { Id = exportId, UserId = otherUserId, ResumeVersionId = versionId, Format = "txt", FilePath = exportPath, ParseText = "Curriculo ATS" });
        await db.SaveChangesAsync();

        var service = new ApplicationResumeService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(userId, taskId, new ApplicationJobResumeRequest(versionId, exportId)));
    }

    [Fact]
    public async Task AssignAsync_RejeitaArquivoExportadoAusente()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var exportPath = Path.Combine(Path.GetTempPath(), $"jobpilot-missing-{Guid.NewGuid():N}.txt");

        db.JobLeads.Add(new JobLead { Id = leadId, UserId = userId, Title = "Dev .NET", Url = "https://example.test/job" });
        db.ApplicationRuns.Add(new ApplicationRun { Id = runId, UserId = userId, Name = "Run" });
        db.ApplicationJobs.Add(new ApplicationJob { Id = taskId, UserId = userId, ApplicationRunId = runId, JobLeadId = leadId, JobUrl = "https://example.test/job" });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Direcionado", PlainText = "Curriculo ATS" });
        db.ResumeExports.Add(new ResumeExport { Id = exportId, UserId = userId, ResumeVersionId = versionId, Format = "txt", FilePath = exportPath, ParseText = "Curriculo ATS" });
        await db.SaveChangesAsync();

        var service = new ApplicationResumeService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(userId, taskId, new ApplicationJobResumeRequest(versionId, exportId)));
        Assert.Contains("Arquivo exportado", error.Message);
    }

    [Fact]
    public async Task AssignToJobLeadAsync_CriaCandidaturaManualQuandoNaoExiste()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.JobLeads.Add(new JobLead { Id = leadId, UserId = userId, Title = "Dev .NET", Url = "https://example.test/job" });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Base", PlainText = "Curriculo ATS" });
        await db.SaveChangesAsync();

        var service = new ApplicationResumeService(db);
        var result = await service.AssignToJobLeadAsync(userId, leadId, new ApplicationJobResumeRequest(versionId));

        Assert.Equal(versionId, result.ResumeVersionId);
        var task = await db.ApplicationJobs.SingleAsync(x => x.JobLeadId == leadId);
        Assert.Equal(JobStatuses.ManualOnly, task.State);
        Assert.Equal(versionId, task.ResumeVersionId);
        Assert.Single(await db.ApplicationRuns.ToListAsync());
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
