using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationResumePreparationService(
    JobPilotDbContext db,
    JobTargetService jobTargetService,
    ResumeTailoringService tailoringService,
    ResumeExportService exportService,
    ApplicationResumeService applicationResumeService)
{
    public async Task PrepareQueuedAsync(Guid userId, IEnumerable<Guid> applicationJobIds)
    {
        foreach (var applicationJobId in applicationJobIds)
            await PrepareOneAsync(userId, applicationJobId);
    }

    private async Task PrepareOneAsync(Guid userId, Guid applicationJobId)
    {
        try
        {
            var task = await db.ApplicationJobs
                .Include(x => x.JobLead)
                .FirstOrDefaultAsync(x => x.Id == applicationJobId && x.UserId == userId);
            if (task?.JobLead is null)
            {
                await RecordEventAsync(userId, applicationJobId, "application.resume_prepare_skipped", "Candidatura ou vaga nao encontrada.");
                return;
            }

            if (task.ResumeExportId is not null && !string.IsNullOrWhiteSpace(task.ResumeFilePath) && File.Exists(task.ResumeFilePath))
                return;

            var baseVersion = await db.ResumeVersions
                .Where(x => x.UserId == userId && !string.IsNullOrWhiteSpace(x.PlainText))
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
            if (baseVersion is null)
            {
                await RecordEventAsync(userId, task.Id, "application.resume_prepare_skipped", "Nenhuma versao base de curriculo disponivel para preparar candidatura.");
                return;
            }

            if (string.IsNullOrWhiteSpace(task.JobLead.Description) || task.JobLead.Description.Trim().Length < 80)
            {
                await RecordEventAsync(userId, task.Id, "application.resume_prepare_skipped", "Descricao da vaga insuficiente para gerar curriculo direcionado.");
                return;
            }

            var target = await jobTargetService.OrganizeAsync(userId, new OrganizeJobTargetRequest(
                task.JobLead.Id,
                "job_lead",
                null,
                null,
                UseAi: false));
            var tailored = await tailoringService.TailorAsync(userId, new TailorResumeRequest(
                baseVersion.Id,
                target.Id,
                UseAi: false));
            var export = await exportService.ExportAsync(userId, tailored.TailoredVersionId, new CreateResumeExportRequest("docx"));
            await applicationResumeService.AssignAsync(userId, task.Id, new ApplicationJobResumeRequest(tailored.TailoredVersionId, export.Id));

            await RecordEventAsync(userId, task.Id, "application.resume_prepared", $"Curriculo DOCX preparado automaticamente. version={tailored.TailoredVersionId}; export={export.Id}.");
        }
        catch (Exception ex)
        {
            await RecordEventAsync(userId, applicationJobId, "application.resume_prepare_failed", ex.Message);
        }
    }

    private async Task RecordEventAsync(Guid userId, Guid applicationJobId, string eventType, string detail)
    {
        db.ExecutionEvents.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = applicationJobId,
            EventType = eventType,
            Detail = detail
        });
        await db.SaveChangesAsync();
    }
}
