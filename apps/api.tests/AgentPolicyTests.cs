using JobPilot.Api.Domain;

namespace JobPilot.Api.Tests;

public class AgentPolicyTests
{
    [Fact]
    public void AgentPolicyDefaultsToConservativeLimits()
    {
        var policy = new AgentPolicy();

        Assert.Equal("conservative", policy.AutoSubmitMode);
        Assert.Equal(ExecutionTargets.Extension, policy.ExecutionTarget);
        Assert.Equal(10, policy.MaxApplicationsPerRun);
        Assert.Equal(50, policy.MaxApplicationsPerDay);
        Assert.Empty(policy.AllowedDomains);
        Assert.Empty(policy.BlockedDomains);
        Assert.False(policy.IsPaused);
    }

    [Fact]
    public void AgentPolicyApplyUpdatesRuntimeControls()
    {
        var policy = new AgentPolicy();

        policy.Apply(new AgentPolicy
        {
            AutoSubmitMode = "review_only",
            ExecutionTarget = "playwright",
            MaxApplicationsPerRun = 3,
            MaxApplicationsPerDay = 9,
            AllowedDomains = ["linkedin.com", "greenhouse.io"],
            BlockedDomains = ["unknown.example"],
            IsPaused = true
        });

        Assert.Equal("review_only", policy.AutoSubmitMode);
        Assert.Equal(ExecutionTargets.LocalDedicatedBrowser, policy.ExecutionTarget);
        Assert.Equal(3, policy.MaxApplicationsPerRun);
        Assert.Equal(9, policy.MaxApplicationsPerDay);
        Assert.Contains("greenhouse.io", policy.AllowedDomains);
        Assert.Contains("unknown.example", policy.BlockedDomains);
        Assert.True(policy.IsPaused);
    }
}
