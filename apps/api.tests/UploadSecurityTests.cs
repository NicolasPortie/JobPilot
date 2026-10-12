using System.IO.Compression;
using System.Text;
using JobPilot.Api.Services;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public sealed class UploadSecurityTests
{
    [Fact]
    public async Task DocxWithMacro_IsRejected()
    {
        var path = CreateDocx(("word/document.xml", "<w:document/>"), ("word/vbaProject.bin", "macro"));
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UploadFilePolicy.ValidateStoredArchiveAsync(path, ".docx"));
            Assert.Contains("macro", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DocxWithExternalLink_IsRejected()
    {
        var rels = """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship TargetMode="External" Target="http://evil.example/x"/></Relationships>""";
        var path = CreateDocx(("word/document.xml", "<w:document/>"), ("word/_rels/document.xml.rels", rels));
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UploadFilePolicy.ValidateStoredArchiveAsync(path, ".docx"));
            Assert.Contains("externo", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CorruptPdf_IsRejectedWithClearMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"corrupt-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, "%PDF-1.4 garbage-bytes-not-a-real-pdf"u8.ToArray());
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => UploadFilePolicy.ValidatePdfReadable(path));
            Assert.Contains("PDF", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportedDocx_ReextractsWithHighCoverage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.docx");
        const string text = "João da Silva\nExperiência com .NET e React.\nContato: joao@example.com";
        try
        {
            await ResumeExportRenderer.WriteDocxAsync(path, text, "ats_standard");
            var reextracted = ResumeExportRenderer.ExtractDocxText(path);
            Assert.True(ResumeExportRenderer.CalculateTokenCoverage(text, reextracted) >= 0.6m);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void AntivirusWarning_WhenNotConfigured()
    {
        var none = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Assert.NotNull(AntivirusPolicy.WarningFor(none));

        var clamav = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Antivirus:Mode"] = "clamav",
        }).Build();
        Assert.Null(AntivirusPolicy.WarningFor(clamav));
    }

    private static string CreateDocx(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.docx");
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }
        return path;
    }
}
