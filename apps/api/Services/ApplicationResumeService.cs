using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ApplicationResumeService(
    IRepository<JobLead> leads,
    IRepository<ApplicationRun> runs,
    IRepository<ApplicationJob> jobs,
    IRepository<ResumeVersion> versions,
    IRepository<ResumeExport> exports,
    IRepository<ExecutionEvent> events,
    IUnitOfWork unitOfWork)
{
    public async Task<ApplicationJobResumeResponse> AssignToJobLeadAsync(Guid userId, Guid jobLeadId, ApplicationJobResumeRequest request)
    {
        var lead = await leads.FirstOrDefaultAsync(x => x.Id == jobLeadId && x.UserId == userId)
            ?? throw new InvalidOperationException("Vaga não encontrada.");

        var task = await jobs.Query()
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
            runs.Add(run);
            jobs.Add(task);
            await unitOfWork.SaveChangesAsync();
        }

        return await AssignAsync(userId, task.Id, request);
    }

    public async Task<ApplicationJobResumeResponse> AssignAsync(Guid userId, Guid applicationJobId, ApplicationJobResumeRequest request)
    {
        var task = await jobs.FirstOrDefaultAsync(x => x.Id == applicationJobId && x.UserId == userId)
            ?? throw new InvalidOperationException("Candidatura não encontrada.");

        var version = await versions.FirstOrDefaultAsync(x => x.Id == request.ResumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");

        ResumeExport? export = null;
        if (request.ResumeExportId is { } exportId)
        {
            export = await exports.FirstOrDefaultAsync(x => x.Id == exportId && x.UserId == userId)
                ?? throw new InvalidOperationException("Exportação de currículo não encontrada.");
            if (export.ResumeVersionId != version.Id)
                throw new InvalidOperationException("Exportação não pertence à versão de currículo informada.");
            if (!File.Exists(export.FilePath))
                throw new InvalidOperationException("Arquivo exportado não encontrado no disco.");
        }

        task.ResumeVersionId = version.Id;
        task.ResumeExportId = export?.Id;
        task.ResumeFilePath = export?.FilePath ?? "";

        events.Add(new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ApplicationJobId = task.Id,
            EventType = "application.resume_assigned",
            Detail = export is null
                ? $"ResumeVersion assigned: {version.Id}."
                : $"ResumeVersion assigned: {version.Id}; ResumeExport assigned: {export.Id}; file={export.FilePath}."
        });

        await unitOfWork.SaveChangesAsync();
        return new ApplicationJobResumeResponse(task.Id, version.Id, export?.Id, task.ResumeFilePath);
    }
}
