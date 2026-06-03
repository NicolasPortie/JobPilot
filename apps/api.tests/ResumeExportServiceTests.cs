using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;

namespace JobPilot.Api.Tests;

public class ResumeExportServiceTests
{
    [Fact]
    public async Task ExportAsync_Txt_CriaArquivoEPersisteAuditoria()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportRoot = Path.Combine(Path.GetTempPath(), "jobpilot-export-tests", Guid.NewGuid().ToString("N"));

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Direcionado",
            TargetRole = "Desenvolvedor .NET",
            TargetCompany = "Acme",
            PlainText = """
            Nicolas Portie

            OBJETIVO
            Desenvolvedor .NET

            RESUMO PROFISSIONAL
            Desenvolvedor com experiencia em APIs REST, ASP.NET Core, SQL e sistemas web.
            """
        });
        await db.SaveChangesAsync();

        var service = new ResumeExportService(db, Config(exportRoot));
        var result = await service.ExportAsync(userId, versionId, new CreateResumeExportRequest("txt"));

        Assert.Equal("txt", result.Format);
        Assert.True(File.Exists(result.FilePath));
        Assert.Contains("Desenvolvedor .NET", await File.ReadAllTextAsync(result.FilePath));
        Assert.Single(await db.ResumeExports.Where(x => x.UserId == userId && x.ResumeVersionId == versionId).ToListAsync());
        Assert.Contains(userId.ToString("N"), result.FilePath);
    }

    [Fact]
    public async Task ExportAsync_Docx_CriaDocumentoAtsSafeComTextoNativo()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportRoot = Path.Combine(Path.GetTempPath(), "jobpilot-export-tests", Guid.NewGuid().ToString("N"));

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Direcionado",
            TargetRole = "Desenvolvedor .NET",
            TargetCompany = "Acme",
            PlainText = """
            Nicolas & Portie

            RESUMO PROFISSIONAL
            APIs REST <ASP.NET Core>, SQL e React.
            """
        });
        await db.SaveChangesAsync();

        var service = new ResumeExportService(db, Config(exportRoot));
        var result = await service.ExportAsync(userId, versionId, new CreateResumeExportRequest("docx"));

        Assert.Equal("docx", result.Format);
        Assert.EndsWith(".docx", result.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.FilePath));

        using var archive = ZipFile.OpenRead(result.FilePath);
        var documentEntry = archive.GetEntry("word/document.xml");
        Assert.NotNull(documentEntry);
        using var reader = new StreamReader(documentEntry.Open());
        var xml = await reader.ReadToEndAsync();

        Assert.Contains("Nicolas &amp; Portie", xml);
        Assert.Contains("APIs REST &lt;ASP.NET Core&gt;, SQL e React.", xml);
        Assert.Contains("<w:cols w:num=\"1\"", xml);
        Assert.DoesNotContain("<w:tbl", xml, StringComparison.OrdinalIgnoreCase);

        var saved = await db.ResumeExports.SingleAsync(x => x.UserId == userId && x.ResumeVersionId == versionId && x.Format == "docx");
        Assert.Contains("Nicolas & Portie", saved.ParseText);
        Assert.Contains("APIs REST <ASP.NET Core>, SQL e React.", saved.ParseText);
        Assert.DoesNotContain("difere do preview", string.Join(" ", result.ParseWarnings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_Pdf_CriaDocumentoAtsSafeComTextoNativoReextraido()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var exportRoot = Path.Combine(Path.GetTempPath(), "jobpilot-export-tests", Guid.NewGuid().ToString("N"));

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Direcionado",
            TargetRole = "Desenvolvedor .NET",
            TargetCompany = "Acme",
            PlainText = """
            Nicolas Portié
            São Paulo, Brasil
            Projetos com dados 日本語 e suporte a moeda €

            RESUMO PROFISSIONAL
            Desenvolvedor .NET com experiência em APIs REST, ASP.NET Core, SQL, React e integração de sistemas.

            SKILLS
            C#, .NET, ASP.NET Core, SQL, React, Git, automação, gestão técnica
            """
        });
        await db.SaveChangesAsync();

        var service = new ResumeExportService(db, Config(exportRoot));
        var result = await service.ExportAsync(userId, versionId, new CreateResumeExportRequest("pdf"));

        Assert.Equal("pdf", result.Format);
        Assert.EndsWith(".pdf", result.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.FilePath));

        var saved = await db.ResumeExports.SingleAsync(x => x.UserId == userId && x.ResumeVersionId == versionId && x.Format == "pdf");
        Assert.Contains("Nicolas Portié", saved.ParseText);
        Assert.Contains("São Paulo", saved.ParseText);
        Assert.Contains("日本語", saved.ParseText);
        Assert.Contains("€", saved.ParseText);
        Assert.Contains("experiência", saved.ParseText);
        Assert.Contains("integração", saved.ParseText);
        Assert.Contains("automação", saved.ParseText);
        Assert.Contains("ASP.NET Core", saved.ParseText);
        Assert.Contains("React", saved.ParseText);
        Assert.DoesNotContain("perdeu muitos termos", string.Join(" ", result.ParseWarnings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_FormatoNaoSuportado_Rejeita()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Base", PlainText = "Curriculo" });
        await db.SaveChangesAsync();

        var service = new ResumeExportService(db, Config(Path.GetTempPath()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(userId, versionId, new CreateResumeExportRequest("xlsx")));
    }

    private static IConfiguration Config(string exportRoot) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Candidate:ResumeExportDirectory"] = exportRoot
            })
            .Build();

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
