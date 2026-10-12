namespace JobPilot.Api.Domain;

public class ApplicationJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationRunId { get; set; }
    public ApplicationRun? ApplicationRun { get; set; }
    public Guid JobLeadId { get; set; }
    public JobLead? JobLead { get; set; }
    public Guid? ResumeVersionId { get; set; }
    public ResumeVersion? ResumeVersion { get; set; }
    public Guid? ResumeExportId { get; set; }
    public ResumeExport? ResumeExport { get; set; }
    public string ResumeFilePath { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string State { get; set; } = JobStatuses.QueuedToApply;
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
