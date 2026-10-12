namespace JobPilot.Api.Application.Contracts;

public record AutomationDiscoverRequest(string Keywords, string Location, int Limit);
