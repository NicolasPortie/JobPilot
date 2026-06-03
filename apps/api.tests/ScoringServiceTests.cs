using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class ScoringServiceTests
{
    [Fact]
    public void Score_RewardsMatchingSkillsSeniorityAndRemoteWork()
    {
        var service = new ScoringService();
        var profile = new CandidateProfile
        {
            TargetRole = "ASP.NET Developer",
            Seniority = "junior",
            Skills = ["C#", "ASP.NET Core", "React", "SQL"]
        };
        var lead = new JobLead
        {
            Title = "Desenvolvedor ASP.NET Junior",
            Description = "C# ASP.NET Core React SQL vaga junior remota",
            Location = "Remote Brazil"
        };

        service.Score(lead, profile);

        Assert.Equal(JobStatuses.Scored, lead.Status);
        Assert.True(lead.Score >= 70);
        Assert.True(lead.IsRemote);
        Assert.Contains("skill", lead.ScoreReason);
    }

    [Fact]
    public void Score_PenalizesSeniorRolesForJuniorProfile()
    {
        var service = new ScoringService();
        var profile = new CandidateProfile
        {
            TargetRole = "ASP.NET Developer",
            Seniority = "junior",
            Skills = ["C#", "ASP.NET Core"]
        };
        var lead = new JobLead
        {
            Title = "Senior ASP.NET Architect",
            Description = "Lead senior role for architecture and management",
            Location = "Sao Paulo"
        };

        service.Score(lead, profile);

        Assert.True(lead.Score < 50);
        Assert.Contains("senioridade acima", lead.ScoreReason);
    }

    [Fact]
    public void Score_PenalizesEmptyDescriptions()
    {
        var service = new ScoringService();
        var lead = new JobLead
        {
            Title = "Backend Developer",
            Location = "Brazil"
        };

        service.Score(lead, null);

        Assert.Contains("descricao vazia", lead.ScoreReason);
    }
}
