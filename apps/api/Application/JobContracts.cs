using JobPilot.Api.Domain;

namespace JobPilot.Api.Application;

public sealed record JobLeadResumeResponse(
    Guid? ResumeVersionId,
    Guid? ResumeExportId);

public sealed record JobLeadResponse(
    Guid Id,
    string Title,
    string Company,
    string Location,
    string Description,
    string Url,
    string Platform,
    int Score,
    string ScoreReason,
    string Status,
    bool IsRemote,
    string FilteredReason,
    DateTimeOffset FoundAt,
    JobLeadResumeResponse? Resume)
{
    public static JobLeadResponse From(JobLead lead, JobLeadResumeResponse? resume = null) =>
        new(lead.Id, lead.Title, lead.Company, lead.Location, lead.Description, lead.Url, lead.Platform, lead.Score, lead.ScoreReason, lead.Status, lead.IsRemote, lead.FilteredReason, lead.FoundAt, resume);
}
