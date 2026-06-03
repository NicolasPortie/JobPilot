using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationResumeService(JobPilotDbContext db)
{
    public async Task<ApplicationJobResumeResponse> AssignToJobLeadAsync(Guid userId, Guid jobLeadId, ApplicationJobResumeRequest request)
    {
        var lead = await db.JobLeads.FirstOrDefaultAsync(x => x.Id == jobLeadId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga nao encontrada.");

        var task = await db.ApplicationJobs
            .Where(x => x.UserId == userId && x.JobLeadId == jobLeadId && x.State != JobStatuses.Applied)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (task is null)
        {
            var run = new ApplicationRun
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = $"Vinculo manual de CV - {lead.Title}",
                State = "manual_resume_link",
                TotalJobs = 1,
                CreatedAt = DateTimeOffset.UtcNow
            };
            task = new ApplicationJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ApplicationRunId = run.Id,
                ApplicationRun = run,
                JobLeadId = lead.Id,
                JobUrl = lead.ApplyUrl.Length > 0 ? lead.ApplyUrl : lead.Url,
                State = JobStatuses.ManualOnly,
                Reason = "Curriculo vinculado manualmente.",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.ApplicationRuns.Add(run);
            db.ApplicationJobs.Add(task);
            await db.SaveChangesAsync();
        }

        return await AssignAsync(userId, task.Id, request);
    }

    public async Task<ApplicationJobResumeResponse> AssignAsync(Guid userId, Guid applicationJobId, ApplicationJobResumeRequest request)
    {
        var task = await db.ApplicationJobs.FirstOrDefaultAsync(x => x.Id == applicationJobId && x.UserId == userId)
            ?? throw new InvalidOperationException("Candidatura nao encontrada.");

        var version = await db.ResumeVersions.FirstOrDefaultAsync(x => x.Id == request.ResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao de curriculo nao encontrada.");

        ResumeExport? export = null;
        if (request.ResumeExportId is { } exportId)
        {
            export = await db.ResumeExports.FirstOrDefaultAsync(x => x.Id == exportId && x.UserId == userId)
                ?? throw new InvalidOperationException("Exportacao de curriculo nao encontrada.");
            if (export.ResumeVersionId != version.Id)
                throw new InvalidOperationException("Exportacao nao pertence a versao de curriculo informada.");
            if (!File.Exists(export.FilePath))
                throw new InvalidOperationException("Arquivo exportado nao encontrado no disco.");
        }

        task.ResumeVersionId = version.Id;
        task.ResumeExportId = export?.Id;
        task.ResumeFilePath = export?.FilePath ?? "";

        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = task.Id,
            EventType = "application.resume_assigned",
            Detail = export is null
                ? $"ResumeVersion assigned: {version.Id}."
                : $"ResumeVersion assigned: {version.Id}; ResumeExport assigned: {export.Id}; file={export.FilePath}."
        });

        await db.SaveChangesAsync();
        return new ApplicationJobResumeResponse(task.Id, version.Id, export?.Id, task.ResumeFilePath);
    }
}
