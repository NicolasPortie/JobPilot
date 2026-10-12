using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class JobDiscovererCredentialTests
{
    [Fact]
    public void SelectLinkedInCredential_RetornaCredencialDoLinkedIn()
    {
        var credentials = new List<PlatformCredential>
        {
            new("indeed", "other@test.local", "pwd1"),
            new("linkedin", "user@test.local", "secret")
        };

        var selected = JobDiscoverer.SelectLinkedInCredential(credentials);

        Assert.NotNull(selected);
        Assert.Equal("user@test.local", selected!.AccountEmail);
        Assert.Equal("secret", selected.Password);
    }

    [Fact]
    public void SelectLinkedInCredential_IgnoraMaiusculasMinusculas()
    {
        var credentials = new List<PlatformCredential>
        {
            new("LinkedIn", "user@test.local", "secret")
        };

        var selected = JobDiscoverer.SelectLinkedInCredential(credentials);

        Assert.NotNull(selected);
        Assert.Equal("user@test.local", selected!.AccountEmail);
    }

    [Fact]
    public void SelectLinkedInCredential_IgnoraCredencialSemSenha()
    {
        var credentials = new List<PlatformCredential>
        {
            new("linkedin", "user@test.local", "")
        };

        Assert.Null(JobDiscoverer.SelectLinkedInCredential(credentials));
    }

    [Fact]
    public void SelectLinkedInCredential_RetornaNullQuandoNaoHaLinkedIn()
    {
        var credentials = new List<PlatformCredential>
        {
            new("indeed", "user@test.local", "secret")
        };

        Assert.Null(JobDiscoverer.SelectLinkedInCredential(credentials));
    }

    [Fact]
    public void SelectLinkedInCredential_RetornaNullQuandoListaNula()
    {
        Assert.Null(JobDiscoverer.SelectLinkedInCredential(null));
    }
}
