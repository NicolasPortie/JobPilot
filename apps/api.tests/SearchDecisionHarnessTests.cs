using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class SearchDecisionHarnessTests
{
    [Fact]
    public void Validate_DowngradesQueueWhenConfidenceIsLow()
    {
        var lead = Lead("Desenvolvedor .NET Junior");
        var filter = Filter();
        var review = new JobFitReviewResponse("queue", 72, "Looks plausible but evidence is thin.", "junior", "remote", "unknown", ["dotnet"], ["description missing"], 0.42m);

        var result = SearchDecisionHarness.Validate(review, lead, filter);

        Assert.Equal("review", result.Decision);
        Assert.Contains("downgraded", result.Reason);
    }

    [Fact]
    public void Validate_IgnoresHardFilterConflictEvenWhenModelQueues()
    {
        var lead = Lead("Senior .NET Architect");
        var filter = Filter();
        var review = new JobFitReviewResponse("queue", 88, "Strong .NET match.", "senior", "remote", "easy_apply", ["dotnet"], ["senior"], 0.91m);

        var result = SearchDecisionHarness.Validate(review, lead, filter);

        Assert.Equal("ignore", result.Decision);
        Assert.Contains("Hard filter conflict", result.Reason);
        Assert.True(result.Confidence >= 0.75m);
    }

    [Fact]
    public void Validate_DoesNotIgnoreWhenRemoteEvidenceIsUnknown()
    {
        var lead = Lead("Engenheiro de Software .NET");
        lead.Location = "Brasil";
        lead.IsRemote = false;
        var filter = Filter();
        var review = new JobFitReviewResponse("review", 64, "Role fits but remote badge is missing.", "unknown", "unknown", "easy_apply", ["dotnet"], ["remote_unknown"], 0.66m);

        var result = SearchDecisionHarness.Validate(review, lead, filter);

        Assert.Equal("review", result.Decision);
        Assert.DoesNotContain("Hard filter conflict", result.Reason);
    }

    [Fact]
    public void UnavailableReview_HoldsJobForReview()
    {
        var result = SearchDecisionHarness.UnavailableReview("empty response");

        Assert.Equal("review", result.Decision);
        Assert.Contains("unavailable", result.Reason);
        Assert.Contains("llm_unavailable", result.Risks);
    }

    private static JobLead Lead(string title) => new()
    {
        Title = title,
        Company = "TestCo",
        Location = "Remote Brazil",
        Description = "ASP.NET Core, C#, SQL.",
        Url = "https://www.linkedin.com/jobs/view/123",
        IsRemote = true,
        Score = 70,
        ScoreReason = "rule score"
    };

    private static JobFilter Filter() => new()
    {
        AllowedSeniorities = ["junior"],
        BlockedSeniorities = ["senior", "sr", "lead", "manager"],
        AllowedRoleKeywords = ["asp.net", ".net", "c#", "full stack", "qa", "tester", "arquiteto asp.net"],
        BlockedRoleKeywords = ["suporte", "support", "infra"],
        AllowedWorkplaceTypes = ["remote"],
        BlockedWorkplaceTypes = ["hybrid", "onsite", "presencial"],
        AllowedApplyTypes = ["easy_apply", "external_form"],
        MinScore = 30,
        AutoIgnoreLowScore = true
    };
}
