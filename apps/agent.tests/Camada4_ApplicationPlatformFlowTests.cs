using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;
using JobPilot.Agent.Platforms.LinkedIn;

namespace JobPilot.Agent.Tests;

public class Camada4_ApplicationPlatformFlowTests
{
    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/123456", "123456")]
    [InlineData("https://www.linkedin.com/jobs/search/?currentJobId=987", "987")]
    [InlineData("https://www.linkedin.com/jobs/view/foo?referenceJobId=654", "654")]
    public void ExtractJobId_ReconheceFormatosDoLinkedIn(string url, string expected)
    {
        Assert.Equal(expected, LinkedInApplicationFlow.ExtractJobId(url));
    }

    [Fact]
    public void ExtractDisplayedJobId_PrefereVagaAtualEmPaginaDeSimilarJobs()
    {
        var url = "https://www.linkedin.com/jobs/collections/similar-jobs/?currentJobId=4421200048&referenceJobId=4420699619";

        Assert.Equal("4421200048", LinkedInApplicationFlow.ExtractDisplayedJobId(url));
        Assert.Equal("4420699619", LinkedInApplicationFlow.ExtractReferenceJobId(url));
    }

    [Fact]
    public void Router_UsaFluxoGenericoQuandoNaoHaPlataformaEspecifica()
    {
        var router = new ApplicationPlatformRouter();
        var lead = new JobLead { Platform = "unknown", Url = "https://example.com/jobs/1" };

        Assert.Same(GenericApplicationPlatformFlow.Instance, router.Resolve(lead));
    }

    [Fact]
    public void SurfaceOpenResult_Success_NaoCarregaFalha()
    {
        var result = ApplicationSurfaceOpenResult.Success;

        Assert.True(result.Opened);
        Assert.Equal("", result.FailureCode);
    }
}
