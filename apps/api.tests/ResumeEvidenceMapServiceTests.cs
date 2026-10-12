using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ResumeEvidenceMapServiceTests
{
    [Fact]
    public async Task BuildAsync_ClassificaProvenAdjacentMissingEUnknown()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            Skills = ["C#", ".NET", "ASP.NET Core", "JavaScript", "SQL"],
            ProfessionalSummary = "Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL."
        });
        db.ResumeDocuments.Add(new ResumeDocument
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Curriculo base"
        });
        var documentId = db.ResumeDocuments.Local.Single().Id;
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Experiencia com C#, ASP.NET Core, APIs REST, SQL, JavaScript e dashboards web."
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Desenvolvedor .NET",
            Company = "Acme",
            Requirements =
            [
                Req(userId, targetId, "technology", "ASP.NET Core", "must_have", "asp.net-core", 1),
                Req(userId, targetId, "technology", "React", "nice_to_have", "react", 2),
                Req(userId, targetId, "technology", "Kubernetes", "must_have", "kubernetes", 3),
                Req(userId, targetId, "technology", "SQL Server", "must_have", "sql-server", 4),
                Req(userId, targetId, "technology", "RabbitMQ", "nice_to_have", "rabbitmq", 5),
                Req(userId, targetId, "technology", "Injecao de dependencia", "nice_to_have", "injecao-de-dependencia", 6),
                Req(userId, targetId, "language", "Ingles avancado", "must_have", "ingles-avancado", 7)
            ]
        });
        await db.SaveChangesAsync();

        var service = TestServices.ResumeEvidenceMapService(db);
        var result = await service.BuildAsync(userId, new BuildResumeEvidenceMapRequest(versionId, targetId));

        Assert.Equal("needs_review", result.Status);
        Assert.Contains(result.Items, x => x.RequirementLabel == "ASP.NET Core" && x.MatchStatus == "proven");
        Assert.Contains(result.Items, x => x.RequirementLabel == "React" && x.MatchStatus == "adjacent");
        Assert.Contains(result.Items, x => x.RequirementLabel == "Kubernetes" && x.MatchStatus == "missing");
        Assert.Contains(result.Items, x => x.RequirementLabel == "SQL Server" && x.MatchStatus == "adjacent");
        Assert.Contains(result.Items, x => x.RequirementLabel == "RabbitMQ" && x.MatchStatus == "adjacent");
        Assert.Contains(result.Items, x => x.RequirementLabel == "Injecao de dependencia" && x.MatchStatus == "adjacent");
        Assert.Contains(result.Items, x => x.RequirementLabel == "Ingles avancado" && x.MatchStatus == "unknown");
        Assert.True(result.EvidenceScore < 100);
        Assert.Single(await db.ResumeEvidenceMapRuns.Where(x => x.UserId == userId).ToListAsync());
        Assert.Equal(7, await db.ResumeRequirementEvidences.CountAsync(x => x.UserId == userId));
    }

    private static JobRequirement Req(Guid userId, Guid targetId, string category, string label, string importance, string keyword, int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobTargetId = targetId,
            Category = category,
            Label = label,
            Importance = importance,
            NormalizedKeyword = keyword,
            ExactPhrases = [label],
            SortOrder = order
        };

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }
}
