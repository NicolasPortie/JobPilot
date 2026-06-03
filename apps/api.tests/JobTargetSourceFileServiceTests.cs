using JobPilot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace JobPilot.Api.Tests;

public class JobTargetSourceFileServiceTests
{
    [Fact]
    public async Task SaveAsync_ValidaESalvaScreenshot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"jobpilot-source-{Guid.NewGuid():N}");
        var service = new JobTargetSourceFileService(new FakeEnvironment(root));
        var bytes = new byte[] { 1, 2, 3, 4 };
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "vaga.png");

        var saved = await service.SaveAsync(Guid.NewGuid(), file);

        Assert.True(File.Exists(saved.Path));
        Assert.Equal("image/png", saved.MediaType);
        var attachment = await JobTargetSourceFileService.ToAttachmentAsync(saved);
        Assert.Equal("image_url", attachment.Type);
        Assert.StartsWith("data:image/png;base64,", attachment.DataUrl);
    }

    [Fact]
    public async Task SaveAsync_RejeitaFormatoInvalido()
    {
        var root = Path.Combine(Path.GetTempPath(), $"jobpilot-source-{Guid.NewGuid():N}");
        var service = new JobTargetSourceFileService(new FakeEnvironment(root));
        await using var stream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(stream, 0, stream.Length, "file", "vaga.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(Guid.NewGuid(), file));
    }

    private sealed class FakeEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
