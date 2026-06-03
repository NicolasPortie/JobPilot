using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class JobTargetServiceTests
{
    [Fact]
    public async Task OrganizeAsync_ComIa_PersisteVagaAlvoERequisitos()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "Desenvolvedor .NET Junior",
            "Acme",
            "Remoto",
            "junior",
            "remote",
            "Vaga para desenvolvimento de APIs com .NET e React.",
            [
                new JobRequirementItem("technology", "ASP.NET Core", "must_have", "Experiencia com ASP.NET Core", "asp.net-core", ["ASP.NET Core"]),
                new JobRequirementItem("technology", "React", "nice_to_have", "Conhecimento em React", "react", ["React"])
            ],
            [],
            0.86m));
        var service = new JobTargetService(db, ai, new HttpClient(), new ResumeOcrService());

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            null,
            "manual_text",
            """
            Acme busca Desenvolvedor .NET Junior remoto.
            Requisitos: Experiencia com ASP.NET Core, APIs REST e SQL.
            Diferencial: Conhecimento em React.
            """,
            "",
            UseAi: true));

        Assert.Equal("Desenvolvedor .NET Junior", result.Title);
        Assert.Equal("Acme", result.Company);
        Assert.Equal("remote", result.Workplace);
        Assert.Equal("organized", result.Status);
        Assert.Equal(2, result.Requirements.Length);
        Assert.Contains(result.Requirements, x => x.Label == "ASP.NET Core" && x.Importance == "must_have");
        Assert.Single(await db.JobTargets.Where(x => x.UserId == userId).ToListAsync());
        Assert.Equal(2, await db.JobRequirements.CountAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task OrganizeAsync_SemIa_UsaFallbackDeterministico()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = new JobTargetService(db, new FakeAiChatService(null), new HttpClient(), new ResumeOcrService());

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            null,
            "manual_text",
            """
            Vaga remota para desenvolvedor junior com foco em C#, ASP.NET Core, APIs REST, SQL, Docker e Git.
            O profissional atuara em projetos de backend, integracoes e manutencao de sistemas web.
            """,
            "",
            UseAi: false));

        Assert.Equal("junior", result.Seniority);
        Assert.Equal("remote", result.Workplace);
        Assert.Contains(result.Requirements, x => x.NormalizedKeyword.Contains("asp.net"));
        Assert.True(result.Confidence > 0m);
    }

    [Fact]
    public async Task OrganizeScreenshotAsync_SalvaUploadEEnviaAnexoParaIa()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "Desenvolvedor C#",
            "Acme",
            "Remoto",
            "junior",
            "remote",
            "Vaga extraida de screenshot.",
            [new JobRequirementItem("technology", "C#", "must_have", "C#", "csharp", ["C#"])],
            [],
            0.8m));
        var service = new JobTargetService(db, ai, new HttpClient(), new ResumeOcrService());
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = new FormFile(stream, 0, stream.Length, "file", "vaga.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await service.OrganizeScreenshotAsync(userId, file, useAi: true);

        Assert.Equal("image_ocr", result.SourceKind);
        Assert.Equal("Desenvolvedor C#", result.Title);
        Assert.NotNull(ai.LastExtractionInput);
        var attachment = Assert.Single(ai.LastExtractionInput!.Attachments ?? []);
        Assert.Equal("image_url", attachment.Type);
        Assert.Equal("image/png", attachment.MediaType);
        Assert.StartsWith("data:image/png;base64,", attachment.DataUrl);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class FakeAiChatService(JobRequirementExtractionResponse? response) : IAiChatService
    {
        public JobRequirementExtractionInput? LastExtractionInput { get; private set; }

        public Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile) =>
            Task.FromResult(new AnswerDraftResponse("", false, "fake"));

        public Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers) =>
            Task.FromResult(new BrowserActionPlanResponse("checkpoint", "", "", "fake", 0m, true));

        public Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter) =>
            Task.FromResult(SearchDecisionHarness.UnavailableReview("fake"));

        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            Task.FromResult<ResumeProfileImportData?>(null);

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input)
        {
            LastExtractionInput = input;
            return Task.FromResult(response);
        }

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsFromAttachmentsAsync(Guid userId, JobRequirementExtractionInput input, AiInputAttachment[] attachments)
        {
            LastExtractionInput = input with { Attachments = attachments };
            return Task.FromResult(response);
        }

        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            Task.FromResult<TailoredResumeGenerationResponse?>(null);

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            Task.FromResult(new PageStateResponse("unknown", "fake", 0m));

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            Task.FromResult(new StepValidationResponse(false, "fake", 0m, true));

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            Task.FromResult(new AnswerDraftResponse("fake", false, "fake"));
    }
}
