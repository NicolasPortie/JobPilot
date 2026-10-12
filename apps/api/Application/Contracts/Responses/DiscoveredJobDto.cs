using JobPilot.Api.Domain;

namespace JobPilot.Api.Application.Contracts;

public record DiscoveredJobDto(string Title, string Company, string Location, string Description, string Url, bool IsRemote)
{
    public JobLead ToLead(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = Title,
        Company = Company,
        Location = Location,
        Description = Description,
        Url = Url,
        IsRemote = IsRemote,
        Platform = "linkedin",
        Status = JobStatuses.Found
    };
}
