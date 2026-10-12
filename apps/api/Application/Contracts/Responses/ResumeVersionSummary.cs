namespace JobPilot.Api.Application.Contracts;

public record ResumeVersionSummary(Guid Id, string Title, string Status, string TargetRole, string TargetCompany, int AtsScore, DateTimeOffset UpdatedAt);
