namespace JobPilot.Api.Application.Contracts;

public record JobRequirementExtractionResponse(string Title, string Company, string Location, string Seniority, string Workplace, string Summary, JobRequirementItem[] Requirements, string[] Warnings, decimal Confidence);
