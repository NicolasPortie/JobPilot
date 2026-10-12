using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Platforms.LinkedIn;

namespace JobPilot.Agent.Tests;

public class DiscoveryDedupAndLocationTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/4012345678/?refId=abc&trk=xyz", "linkedin:4012345678")]
    [InlineData("https://br.linkedin.com/jobs/view/4012345678", "linkedin:4012345678")]
    [InlineData("https://www.linkedin.com/jobs/view/4012345678/", "linkedin:4012345678")]
    public void CanonicalUrl_SameJobDifferentUrls_ProduceSameKey(string url, string expected)
    {
        Assert.Equal(expected, JobDiscoverer.CanonicalUrl(url));
    }

    [Fact]
    public void CanonicalUrl_WithoutJobId_NormalizesQueryAndTrailingSlash()
    {
        Assert.Equal(
            "https://example.com/careers/123",
            JobDiscoverer.CanonicalUrl("https://example.com/careers/123/?utm=1"));
    }

    [Fact]
    public void TitleCompanyKey_IgnoresAccentsCaseAndPunctuation()
    {
        var a = new DiscoveredJobDto("Desenvolvedor(a) .NET Sênior", "Acme Tecnologia", "Brasil", "", "https://x/1", false);
        var b = new DiscoveredJobDto("DESENVOLVEDOR A  NET SENIOR", "acme   tecnologia", "São Paulo", "", "https://x/2", false);

        Assert.Equal(JobDiscoverer.TitleCompanyKey(a), JobDiscoverer.TitleCompanyKey(b));
    }

    [Fact]
    public void TitleCompanyKey_DistinguishesDifferentCompanies()
    {
        var a = new DiscoveredJobDto(".NET Developer", "Empresa A", "Brasil", "", "https://x/1", false);
        var b = new DiscoveredJobDto(".NET Developer", "Empresa B", "Brasil", "", "https://x/2", false);

        Assert.NotEqual(JobDiscoverer.TitleCompanyKey(a), JobDiscoverer.TitleCompanyKey(b));
    }

    [Theory]
    [InlineData("Brasil", "106057199")]
    [InlineData("brazil", "106057199")]
    [InlineData("São Paulo", "105871508")]
    [InlineData("sao paulo", "105871508")]
    [InlineData("Portugal", "100364837")]
    public void ResolveGeoId_KnownLocations_ReturnGeoId(string location, string expected)
    {
        Assert.Equal(expected, LinkedInDiscoveryService.ResolveGeoId(location));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Cidade Inexistente XYZ")]
    public void ResolveGeoId_UnknownLocations_ReturnNull(string? location)
    {
        Assert.Null(LinkedInDiscoveryService.ResolveGeoId(location));
    }

    [Fact]
    public void SelectLinkedInCredential_PicksActiveLinkedInWithEmailAndPassword()
    {
        var credentials = new[]
        {
            new PlatformCredential("indeed", "user@example.com", "pass"),
            new PlatformCredential("linkedin", "linkedin@example.com", "secret"),
        };

        var selected = JobDiscoverer.SelectLinkedInCredential(credentials);

        Assert.NotNull(selected);
        Assert.Equal("linkedin@example.com", selected!.AccountEmail);
        Assert.Equal("secret", selected.Password);
    }

    [Fact]
    public void SelectLinkedInCredential_WithoutPassword_IsNotALoginCredential()
    {
        var credentials = new[]
        {
            new PlatformCredential("linkedin", "linkedin@example.com", ""),
        };

        var selected = JobDiscoverer.SelectLinkedInCredential(credentials);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectLinkedInCredential_WithNullList_ReturnsNull()
    {
        Assert.Null(JobDiscoverer.SelectLinkedInCredential(null));
    }
}
