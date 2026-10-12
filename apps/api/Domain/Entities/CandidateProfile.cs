namespace JobPilot.Api.Domain;

public class CandidateProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Cpf { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "Brasil";
    public string LinkedinUrl { get; set; } = "";
    public string GithubUrl { get; set; } = "";
    public string PortfolioUrl { get; set; } = "";
    public string ProfessionalSummary { get; set; } = "";
    public string[] Skills { get; set; } = [];
    public string TargetRole { get; set; } = "";
    public string Seniority { get; set; } = "junior";
    public string SalaryExpectation { get; set; } = "";
    public string ResumePath { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(CandidateProfile input)
    {
        FullName = input.FullName;
        Cpf = input.Cpf;
        Email = input.Email;
        Phone = input.Phone;
        City = input.City;
        State = input.State;
        Country = input.Country;
        LinkedinUrl = input.LinkedinUrl;
        GithubUrl = input.GithubUrl;
        PortfolioUrl = input.PortfolioUrl;
        ProfessionalSummary = input.ProfessionalSummary;
        Skills = input.Skills;
        TargetRole = input.TargetRole;
        Seniority = input.Seniority;
        SalaryExpectation = input.SalaryExpectation;
        ResumePath = input.ResumePath;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
