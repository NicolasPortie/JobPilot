using JobPilot.Agent;

namespace JobPilot.Agent.Tests;

public class AgentOptionsTests
{
    [Fact]
    public void Parse_EnablesDryRunFromFlag()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc", "--dry-run"]);

        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_DisablesAutoSubmitFromExplicitFalseValue()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc", "--auto-submit", "false"]);

        Assert.False(options.AutoSubmit);
    }

    [Fact]
    public void Parse_EnablesAutoSubmitFromExplicitTrueValue()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc", "--auto-submit", "true"]);

        Assert.True(options.AutoSubmit);
    }

    [Fact]
    public void Parse_EnablesAutoSubmitFromFlagWithoutValue()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc", "--auto-submit"]);

        Assert.True(options.AutoSubmit);
    }

    [Fact]
    public void Parse_DisablesHeadlessFromExplicitFalseValue()
    {
        var options = AgentOptions.Parse(["discover", "--token", "abc", "--headless", "false"]);

        Assert.False(options.Headless);
    }

    [Fact]
    public void Parse_KeepsTokenAndMode()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc"]);

        Assert.Equal("run", options.Mode);
        Assert.Equal("abc", options.Token);
    }

    [Fact]
    public void Parse_UsesPlaywrightDriverByDefault()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc"]);

        Assert.Equal("playwright", options.BrowserDriverMode);
    }

    [Fact]
    public void Parse_AcceptsExtensionDriverFlag()
    {
        var options = AgentOptions.Parse(["run", "--token", "abc", "--browser-driver", "extension"]);

        Assert.Equal("extension", options.BrowserDriverMode);
    }

    [Fact]
    public void Parse_AcceptsBrowserProfileFlag()
    {
        var profile = Path.Combine(Path.GetTempPath(), "jobpilot-profile-test");

        var options = AgentOptions.Parse(["run", "--token", "abc", "--browser-profile", profile]);

        Assert.Equal(profile, options.BrowserProfilePath);
    }

    [Fact]
    public void Parse_UsesBrowserProfileEnvironmentVariable()
    {
        var previous = Environment.GetEnvironmentVariable("JOBPILOT_BROWSER_PROFILE");
        var profile = Path.Combine(Path.GetTempPath(), "jobpilot-profile-env-test");
        Environment.SetEnvironmentVariable("JOBPILOT_BROWSER_PROFILE", profile);
        try
        {
            var options = AgentOptions.Parse(["run", "--token", "abc"]);

            Assert.Equal(profile, options.BrowserProfilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JOBPILOT_BROWSER_PROFILE", previous);
        }
    }
}
