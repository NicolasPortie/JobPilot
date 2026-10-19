using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public sealed class ResumeAnalysisSummaryBuilderTests
{
    private static ResumeRequirementEvidenceDto Item(string label, string status, int order, string category = "skill") =>
        new(Guid.NewGuid(), Guid.NewGuid(), label, category, "must_have", status, status == "proven" ? $"{label} no currículo" : "", "", "", 0.9m, order);

    private static ResumeEvidenceMapDetail Map(params ResumeRequirementEvidenceDto[] items) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ready", 70, 0.8m, [], items, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(80, "high", "Alta aderência")]
    [InlineData(75, "high", "Alta aderência")]
    [InlineData(60, "good", "Boa aderência")]
    [InlineData(55, "good", "Boa aderência")]
    [InlineData(54, "low", "Baixa aderência")]
    public void ForTarget_ClassifiesAdherenceByScore(int score, string level, string badge)
    {
        var summary = ResumeAnalysisSummaryBuilder.ForTarget(score, Map(Item("SQL", "proven", 0)), [], []);

        Assert.Equal(level, summary.Adherence.Level);
        Assert.Equal(badge, summary.Adherence.BadgeLabel);
    }

    [Fact]
    public void ForTarget_MapsEvidenceStatusAndCountsProvenRequirements()
    {
        var summary = ResumeAnalysisSummaryBuilder.ForTarget(
            70,
            Map(Item("SQL", "proven", 0), Item("Docker", "adjacent", 1), Item("Python", "missing", 2), Item("Inglês", "unknown", 3, "language")),
            [],
            []);

        Assert.Equal(["proven", "adjacent", "missing", "missing"], summary.Requirements.Select(x => x.Status));
        Assert.Equal(["SQL"], summary.ProvenLabels);
        Assert.Contains("1 de 4 requisitos comprovados", summary.Adherence.Message);
        Assert.Equal("SQL no currículo", summary.Requirements[0].EvidenceText);
    }

    [Fact]
    public void ForTarget_SuggestsOnlyMissingSkillsTheProfileDoesNotHave()
    {
        var summary = ResumeAnalysisSummaryBuilder.ForTarget(
            50,
            Map(Item("Python", "missing", 0), Item("Docker", "missing", 1), Item("python", "missing", 2), Item("Inglês", "missing", 3, "language"), Item("SQL", "proven", 4)),
            ["docker"],
            []);

        Assert.Equal(["Python"], summary.MissingSuggestions);
    }

    [Fact]
    public void ForTarget_NamesKeywordsAndBaseProjectsInInsights()
    {
        var projects = new ResumeSection { SectionType = "projects", ContentJson = """{"items":[{"name":"JobPilot"},{"name":"","role":"Líder"},{"name":"Terceiro"}]}""" };

        var summary = ResumeAnalysisSummaryBuilder.ForTarget(
            60,
            Map(Item("Kubernetes", "missing", 0), Item("Docker", "adjacent", 1)),
            [],
            [projects]);

        Assert.Equal(["results", "keywords", "projects"], summary.Insights.Select(x => x.Key));
        Assert.Contains("Kubernetes e Docker", summary.Insights[1].Text);
        Assert.Contains("JobPilot e Líder", summary.Insights[2].Text);
        Assert.DoesNotContain("Terceiro", summary.Insights[2].Text);
    }

    [Fact]
    public void ForTarget_IgnoresUnreadableProjectContent()
    {
        var broken = new ResumeSection { SectionType = "projects", ContentJson = "{não é json" };

        var summary = ResumeAnalysisSummaryBuilder.ForTarget(60, Map(Item("SQL", "proven", 0)), [], [broken]);

        Assert.Contains("Destaque projetos que demonstrem", summary.Insights[2].Text);
    }

    [Fact]
    public void ForGeneric_HasStructureScoreButNoRequirements()
    {
        var summary = ResumeAnalysisSummaryBuilder.ForGeneric(80);

        Assert.Equal("Estrutura sólida", summary.Adherence.BadgeLabel);
        Assert.Empty(summary.Requirements);
        Assert.Empty(summary.Insights);
    }
}
