using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ResumeReviewAnswerServiceTests
{
    [Fact]
    public async Task SaveAsync_CriaResposta()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = TestServices.ResumeReviewAnswerService(db, new FakeAiChatService());

        var answer = await service.SaveAsync(userId, new SaveReviewAnswerRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Ingles avancado", "Tem ingles?", "Tenho ingles tecnico", "proven", "Curso de ingles"));

        Assert.Equal("proven", answer.AnswerType);
        Assert.Equal("Tenho ingles tecnico", answer.AnswerValue);
    }

    [Fact]
    public async Task SaveAsync_AtualizaRespostaExistente()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var service = TestServices.ResumeReviewAnswerService(db, new FakeAiChatService());

        await service.SaveAsync(userId, new SaveReviewAnswerRequest(null, reqId, "Ingles", "Tem?", "Sim", "proven"));
        var updated = await service.SaveAsync(userId, new SaveReviewAnswerRequest(null, reqId, "Ingles", "Tem?", "Nao", "missing"));

        Assert.Equal("missing", updated.AnswerType);
        Assert.Single(await db.ResumeReviewAnswers.Where(x => x.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task ListAsync_RetornaRespostas()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = TestServices.ResumeReviewAnswerService(db, new FakeAiChatService());

        await service.SaveAsync(userId, new SaveReviewAnswerRequest(null, Guid.NewGuid(), "A", "Q1", "S1", "proven"));
        await service.SaveAsync(userId, new SaveReviewAnswerRequest(null, Guid.NewGuid(), "B", "Q2", "S2", "missing"));

        var list = await service.ListAsync(userId);
        Assert.Equal(2, list.Length);
    }

    [Fact]
    public async Task RegenerateAsync_UsaScorePonderadoDoEvidenceMap()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var csharpReqId = Guid.NewGuid();
        var kubeReqId = Guid.NewGuid();
        var englishReqId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            Email = "nicolas@example.com",
            Skills = ["C#", ".NET"],
            ProfessionalSummary = "Desenvolvedor .NET com experiencia em APIs."
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Experiencia comprovada com C# e .NET em APIs REST."
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Dev .NET",
            Requirements =
            [
                Req(userId, targetId, csharpReqId, "technology", "C#", "must_have", "c#", 1),
                Req(userId, targetId, kubeReqId, "technology", "Kubernetes", "must_have", "kubernetes", 2),
                Req(userId, targetId, englishReqId, "language", "Ingles avancado", "nice_to_have", "ingles-avancado", 3)
            ]
        });
        await db.SaveChangesAsync();

        var evidenceService = TestServices.ResumeEvidenceMapService(db);
        var service = TestServices.ResumeReviewAnswerService(db, new FakeAiChatService());
        var answer = await service.SaveAsync(userId, new SaveReviewAnswerRequest(null, kubeReqId, "Kubernetes", "Tem Kubernetes?", "Atuei com Docker e deploys em ambientes conteinerizados.", "adjacent", "Deploys com Docker"));

        var result = await service.RegenerateAsync(userId, new RegenerateAfterAnswersRequest([answer.Id], versionId, targetId, "ats_classic", false));

        var map = await db.ResumeEvidenceMapRuns
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == result.NewEvidenceMapRunId);
        var expected = ResumeEvidenceScoreCalculator.Calculate(map.Items);

        Assert.Equal(expected.Score, map.EvidenceScore);
        Assert.Equal(expected.Confidence, map.Confidence);
        Assert.Equal(expected.Status, map.Status);
        Assert.NotEqual(33, map.EvidenceScore);
        Assert.Equal(result.NewTailoredVersionId, result.TailoredResume.TailoredVersionId);
        Assert.Equal(result.AtsScore, result.TailoredResume.AtsScore);
        Assert.Equal(result.AtsScore, result.TailoredResume.ScoreComponents.Sum(x => x.Score));
    }

    private static JobRequirement Req(Guid userId, Guid targetId, Guid id, string category, string label, string importance, string keyword, int order) =>
        new()
        {
            Id = id,
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

    private sealed class FakeAiChatService : IAiChatService
    {
        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            Task.FromResult<ResumeProfileImportData?>(null);
        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input) =>
            Task.FromResult<JobRequirementExtractionResponse?>(null);
        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            Task.FromResult<TailoredResumeGenerationResponse?>(null);
    }
}
