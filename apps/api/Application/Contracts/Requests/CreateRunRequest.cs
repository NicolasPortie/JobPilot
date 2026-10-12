namespace JobPilot.Api.Application.Contracts;

public record CreateRunRequest(string Name, Guid[] JobLeadIds);
