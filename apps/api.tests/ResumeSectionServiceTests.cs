using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ResumeSectionServiceTests
{
    [Fact]
    public async Task DeleteAsync_RemovesDeletedContentFromPlainText()
    {
        await using var db = CreateDb();
        var version = await CreateVersionAsync(db);
        var service = TestServices.ResumeSectionService(db);
        var removed = await service.AddAsync(version.UserId, version.Id, new("summary", "Resumo", "{\"text\":\"Conteudo removido\"}"));
        await service.AddAsync(version.UserId, version.Id, new("skills", "Competencias", "{\"text\":\"Conteudo mantido\"}"));

        await service.DeleteAsync(version.UserId, removed.Id);
        db.ChangeTracker.Clear();
        var saved = await db.ResumeVersions.SingleAsync(x => x.Id == version.Id);

        Assert.DoesNotContain("Conteudo removido", saved.PlainText);
        Assert.Contains("Conteudo mantido", saved.PlainText);
    }

    [Fact]
    public async Task DeleteAsync_LastSectionClearsPlainText()
    {
        await using var db = CreateDb();
        var version = await CreateVersionAsync(db);
        var service = TestServices.ResumeSectionService(db);
        var section = await service.AddAsync(version.UserId, version.Id, new("summary", "Resumo", "{\"text\":\"Conteudo removido\"}"));

        await service.DeleteAsync(version.UserId, section.Id);
        db.ChangeTracker.Clear();

        Assert.Empty((await db.ResumeVersions.SingleAsync(x => x.Id == version.Id)).PlainText);
    }

    [Fact]
    public async Task UpdateAsync_SortOrderIsReflectedInPlainText()
    {
        await using var db = CreateDb();
        var version = await CreateVersionAsync(db);
        var service = TestServices.ResumeSectionService(db);
        await service.AddAsync(version.UserId, version.Id, new("summary", "Resumo", "{\"text\":\"Resumo profissional\"}"));
        var skills = await service.AddAsync(version.UserId, version.Id, new("skills", "Competencias", "{\"text\":\"C#\"}"));

        await service.UpdateAsync(version.UserId, skills.Id, new(SortOrder: 0));
        db.ChangeTracker.Clear();
        var saved = await db.ResumeVersions.SingleAsync(x => x.Id == version.Id);

        Assert.True(saved.PlainText.IndexOf("COMPETÊNCIAS", StringComparison.Ordinal) < saved.PlainText.IndexOf("RESUMO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddAsync_CriaSecao_E_RebuildaPlainText()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Texto inicial"
        });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var section = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("experience", "Experiencia", "[{\"title\":\"Dev\",\"company\":\"Acme\"}]"));

        Assert.Equal("experience", section.SectionType);
        Assert.Equal("Experiencia", section.Title);

        var version = await db.ResumeVersions.FindAsync(versionId);
        Assert.NotNull(version);
        Assert.Contains("EXPERIÊNCIA", version.PlainText);
    }

    [Fact]
    public async Task UpdateAsync_AlteraSecao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Texto"
        });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var added = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("skills", "Competencias", "{\"items\":[\"C#\"]}"));
        var updated = await service.UpdateAsync(userId, added.Id, new UpdateResumeSectionRequest("Novas Competencias", "{\"items\":[\"C#\",\".NET\"]}", 5));

        Assert.Equal("Novas Competencias", updated.Title);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task DeleteAsync_RemoveSecao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Texto"
        });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var added = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("skills", "Competencias", "{\"items\":[\"C#\"]}"));
        await service.DeleteAsync(userId, added.Id);

        Assert.Empty(await db.ResumeSections.Where(x => x.ResumeVersionId == versionId).ToListAsync());
    }

    [Fact]
    public async Task ReorderAsync_ReordenaSecoes_E_RebuildaPlainText()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Texto"
        });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var first = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("summary", "Resumo", "{\"text\":\"Resumo profissional\"}"));
        var second = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("skills", "Competencias", "{\"items\":[\"C#\"]}"));
        var third = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("experience", "Experiencia", "{\"items\":[\"Dev\"]}"));

        var reordered = await service.ReorderAsync(userId, versionId, new ReorderResumeSectionsRequest([third.Id, first.Id, second.Id]));

        Assert.Equal([third.Id, first.Id, second.Id], reordered.Select(x => x.Id).ToArray());
        Assert.Equal([10, 20, 30], reordered.Select(x => x.SortOrder).ToArray());

        var version = await db.ResumeVersions.SingleAsync(x => x.Id == versionId);
        Assert.True(version.PlainText.IndexOf("EXPERIÊNCIA", StringComparison.Ordinal) < version.PlainText.IndexOf("RESUMO", StringComparison.Ordinal));
        Assert.True(version.PlainText.IndexOf("RESUMO", StringComparison.Ordinal) < version.PlainText.IndexOf("COMPETÊNCIAS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReorderAsync_RejeitaIdsDuplicados()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Base" });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var section = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("summary", "Resumo", "{\"text\":\"Resumo\"}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReorderAsync(userId, versionId, new ReorderResumeSectionsRequest([section.Id, section.Id])));
    }

    [Fact]
    public async Task ReorderAsync_RejeitaSecaoDeOutraVersao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var otherVersionId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.AddRange(
            new ResumeVersion { Id = versionId, UserId = userId, ResumeDocumentId = documentId, Title = "Base" },
            new ResumeVersion { Id = otherVersionId, UserId = userId, ResumeDocumentId = documentId, Title = "Outra" });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeSectionService(db);
        var section = await service.AddAsync(userId, versionId, new CreateResumeSectionRequest("summary", "Resumo", "{\"text\":\"Resumo\"}"));
        var other = await service.AddAsync(userId, otherVersionId, new CreateResumeSectionRequest("skills", "Competencias", "{\"items\":[\"C#\"]}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReorderAsync(userId, versionId, new ReorderResumeSectionsRequest([section.Id, other.Id])));
    }

    private static async Task<ResumeVersion> CreateVersionAsync(JobPilotDbContext db)
    {
        var document = new ResumeDocument { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Base" };
        var version = new ResumeVersion { Id = Guid.NewGuid(), UserId = document.UserId, ResumeDocumentId = document.Id, Title = "Base", PlainText = "Texto inicial" };
        db.ResumeDocuments.Add(document);
        db.ResumeVersions.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
