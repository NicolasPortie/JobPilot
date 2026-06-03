using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class ResumeOcrServiceTests
{
    [Fact]
    public void GetStatus_RetornaDisponibilidade()
    {
        var service = new ResumeOcrService();
        var status = service.GetStatus();

        Assert.NotNull(status);
        Assert.NotNull(status.Tesseract);
        Assert.NotNull(status.OcrMyPdf);
        Assert.NotNull(status.Notes);
    }

    [Fact]
    public void RunOcr_ArquivoInexistente_RetornaErro()
    {
        var service = new ResumeOcrService();
        var result = service.RunOcr(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Warnings);
    }
}
