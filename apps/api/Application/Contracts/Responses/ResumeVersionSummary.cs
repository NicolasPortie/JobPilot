namespace JobPilot.Api.Application.Contracts;

/// <summary>Kind: "base" (importada do arquivo), "tailored" (adaptada a uma vaga), "ats" (geral sem vaga) ou "other".</summary>
public record ResumeVersionSummary(Guid Id, string Title, string Status, string TargetRole, string TargetCompany, int AtsScore, DateTimeOffset UpdatedAt, string Kind = "other");
