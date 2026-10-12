namespace JobPilot.Api.Application.Contracts;

public record AutomationApplyRequest(bool AutoSubmit, int MaxApplications);
