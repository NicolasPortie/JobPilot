namespace JobPilot.Api.Application.Contracts;

public record ResumeProfileImportData(
    string FullName,
    string Email,
    string Phone,
    string City,
    string State,
    string Country,
    string LinkedinUrl,
    string GithubUrl,
    string PortfolioUrl,
    string ProfessionalSummary,
    string[] Skills,
    string TargetRole,
    string Seniority);
