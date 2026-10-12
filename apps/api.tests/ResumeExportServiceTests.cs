using System.IO.Compression;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class ResumeExportServiceTests
{
    [Fact]
    public async Task ExportAsync_RepeatedExportsPreservePreviousFiles()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var document = new ResumeDocument { Id = Guid.NewGuid(), UserId = userId, Title = "Base" };
        var version = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = document.Id,
            Title = "Base",
            TargetRole = new string('A', 120),
            PlainText = "Primeiro conteudo"
        };
        db.ResumeDocuments.Add(document);
        db.ResumeVersions.Add(version);
        await db.SaveChangesAsync();
        var root = Path.Combine(Path.GetTempPath(), "jobpilot-export-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ResumeExportService(db, Config(root));
            var first = await service.ExportAsync(userId, version.Id, new("txt"));
            version.PlainText = "Segundo conteudo";
            await db.SaveChangesAsync();
            var second = await service.ExportAsync(userId, version.Id, new("txt"));
            var firstFile = (await db.ResumeExports.SingleAsync(x => x.Id == first.Id)).FilePath;
            var secondFile = (await db.ResumeExports.SingleAsync(x => x.Id == second.Id)).FilePath;

            Assert.NotEqual(firstFile, secondFile);
            Assert.Contains("Primeiro conteudo", await File.ReadAllTextAsync(firstFile));
            Assert.Contains("Segundo conteudo", await File.ReadAllTextAsync(secondFile));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

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
        var exportedFile = (await db.ResumeExports.SingleAsync(x => x.Id == result.Id)).FilePath;

        Assert.Equal("txt", result.Format);
        Assert.True(File.Exists(exportedFile));
        Assert.Contains("Desenvolvedor .NET", await File.ReadAllTextAsync(exportedFile));
        Assert.Single(await db.ResumeExports.Where(x => x.UserId == userId && x.ResumeVersionId == versionId).ToListAsync());
        Assert.Contains(userId.ToString("N"), exportedFile);
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
        var exportedFile = (await db.ResumeExports.SingleAsync(x => x.Id == result.Id)).FilePath;

        Assert.Equal("docx", result.Format);
        Assert.EndsWith(".docx", exportedFile, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(exportedFile));

        using var archive = ZipFile.OpenRead(exportedFile);
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
        var exportedFile = (await db.ResumeExports.SingleAsync(x => x.Id == result.Id)).FilePath;

        Assert.Equal("pdf", result.Format);
        Assert.EndsWith(".pdf", exportedFile, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(exportedFile));

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
    public async Task ExportAsync_Pdf_Longo_AvisaNumeroDePaginas()
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
            Title = "Longo",
            PlainText = string.Join("\n", Enumerable.Range(1, 300).Select(i => $"Linha {i}: experiencia com C#, .NET, APIs REST, SQL e React em projetos corporativos.")),
        });
        await db.SaveChangesAsync();

        var service = new ResumeExportService(db, Config(exportRoot));
        var result = await service.ExportAsync(userId, versionId, new CreateResumeExportRequest("pdf"));

        Assert.Contains("páginas", string.Join(" ", result.ParseWarnings), StringComparison.OrdinalIgnoreCase);
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
