namespace JobPilot.Api.Application.Contracts;

public record SiteWorkflowMemoryRequest(string Domain, string PatternKey, string ActionJson, bool Success);
