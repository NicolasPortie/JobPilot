namespace JobPilot.Api.Application.Contracts;

public record JobTargetDetail(Guid Id, string SourceKind, string SourceUrl, string Title, string Company, string Location, string Seniority, string Workplace, string Summary, string Status, decimal Confidence, string[] Warnings, JobRequirementDto[] Requirements, DateTimeOffset UpdatedAt);
