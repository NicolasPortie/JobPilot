using System.Net;
using System.Text;
using System.Text.Json;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobPilot.Api.Tests;

public class ProfileImportServiceTests
{
    [Fact]
    public async Task ImportAsync_CurriculoCompleto_EnriquecePerfilERespostas()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(resumePath, """
            Nicolas Portie
            Desenvolvedor Full Stack em Sao Paulo/SP
            Portfolio: https://portfolio.example.com
            GitHub: https://github.com/nicolas
            Experiencia com TypeScript, JavaScript, React, HTML5, CSS3, Java 17,
            Spring Boot, ASP.NET Core, APIs REST, SQL, PostgreSQL, Git e Docker.
            Projetos com orientacao a objetos e integracoes web.
            """);

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            ResumePath = resumePath,
            Skills = ["C#", ".NET"]
        });
        db.ReusableAnswers.Add(new ReusableAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestionKey = "spring_boot_solido",
            QuestionText = "Possui conhecimento sólido da base do ecossistema Spring Boot?",
            AnswerValue = "Não",
            Category = "manual"
        });
        await db.SaveChangesAsync();

        try
        {
            var service = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), new HttpClient());

            var result = await service.ImportAsync(userId, new ProfileImportRequest(FetchPublicLinks: false, UseAi: false));

            Assert.Contains("Nicolas Portie", result.ExtractedText);
            Assert.Contains("ASP.NET Core", result.ExtractedText);
            Assert.Contains("TypeScript", result.Profile.Skills);
            Assert.Contains("JavaScript", result.Profile.Skills);
            Assert.Contains("Java", result.Profile.Skills);
            Assert.Contains("Spring Boot", result.Profile.Skills);
            Assert.Contains("API REST", result.Profile.Skills);
            Assert.Contains("HTML5", result.Profile.Skills);
            Assert.Contains("CSS3", result.Profile.Skills);
            Assert.Single(await db.ResumeImports.Where(x => x.UserId == userId).ToListAsync());
            var document = Assert.Single(await db.ResumeDocuments.Include(x => x.Versions).Where(x => x.UserId == userId).ToListAsync());
            var version = Assert.Single(document.Versions);
            Assert.Contains("Currículo base", document.Title);
            Assert.Contains("Versão base", version.Title);
            Assert.False(string.IsNullOrWhiteSpace(version.PlainText));
            Assert.Contains(await db.ResumeSections.Where(x => x.ResumeVersionId == version.Id).ToListAsync(), x => x.SectionType == "skills");

            var answers = await db.ReusableAnswers.Where(x => x.UserId == userId).ToListAsync();
            Assert.Contains(answers, x => x.QuestionText.Contains("Spring Boot") && x.AnswerValue == "Sim");
            Assert.Contains(answers, x => x.QuestionText.Contains("TypeScript") && x.AnswerValue == "Sim");
            Assert.Contains(answers, x => x.QuestionText.Contains("JavaScript") && x.AnswerValue == "Sim");
            Assert.Single(answers, x => x.QuestionText.Contains("Spring Boot"));
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task ImportAsync_PortfolioComGithub_UsaReposPublicosComoEvidencia()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(resumePath, "Portfolio: https://portfolio.example.com");

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumePath = resumePath
        });
        await db.SaveChangesAsync();

        try
        {
            var http = new HttpClient(new FakeHttpHandler(request =>
            {
                var url = request.RequestUri?.ToString() ?? "";
                if (url == "https://portfolio.example.com/")
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<a href=\"https://github.com/nicolas\">GitHub</a>")
                    };
                if (url.StartsWith("https://api.github.com/users/nicolas/repos", StringComparison.OrdinalIgnoreCase))
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                            [
                              {"name":"frontend","language":"TypeScript","topics":["react"],"description":"Dashboard com JavaScript, HTML5 e CSS3"},
                              {"name":"backend","language":"Java","topics":["spring-boot","api-rest"],"description":"API REST com Spring Boot"}
                            ]
                            """)
                    };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
            }));
            var service = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), http);

            var result = await service.ImportAsync(userId, new ProfileImportRequest(FetchPublicLinks: true, UseAi: false));

            Assert.Contains("TypeScript", result.Profile.Skills);
            Assert.Contains("JavaScript", result.Profile.Skills);
            Assert.Contains("Java", result.Profile.Skills);
            Assert.Contains("Spring Boot", result.Profile.Skills);
            Assert.Contains("API REST", result.Profile.Skills);
            Assert.Contains(await db.ReusableAnswers.ToListAsync(), x => x.QuestionText.Contains("Spring Boot") && x.AnswerValue == "Sim");
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task ImportAsync_NaoRebaixaRespostaConfirmadaParaNao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(resumePath, "Curriculo sem mencao textual de Spring Boot.");

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumePath = resumePath,
            Skills = ["Spring Boot", "TypeScript"]
        });
        db.ReusableAnswers.Add(new ReusableAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestionKey = "typescript",
            QuestionText = "Possui conhecimento/experiência com TypeScript?",
            AnswerValue = "Sim",
            Category = "manual"
        });
        await db.SaveChangesAsync();

        try
        {
            var service = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), new HttpClient());

            await service.ImportAsync(userId, new ProfileImportRequest(FetchPublicLinks: false, UseAi: false));

            Assert.Contains(await db.ReusableAnswers.ToListAsync(), x => x.QuestionKey == "typescript" && x.AnswerValue == "Sim");
            Assert.Contains(await db.ReusableAnswers.ToListAsync(), x => x.QuestionText.Contains("Spring Boot") && x.AnswerValue == "Sim");
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task ImportAsync_TextoNativo_RetornaDiagnosticoDeExtracao()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-resume-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(resumePath, """
            Nicolas Portie
            Email: nicolas@example.com
            Telefone: (11) 99999-9999
            Experiencia profissional com ASP.NET Core, React, TypeScript, SQL e Docker.
            Projetos de APIs REST, dashboards e automacao de candidaturas.
            Formacao em tecnologia e habilidades com Git, PostgreSQL e testes.
            """);

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumePath = resumePath
        });
        await db.SaveChangesAsync();

        try
        {
            var service = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), new HttpClient());

            var result = await service.ImportAsync(userId, new ProfileImportRequest(FetchPublicLinks: false, UseAi: false));

            Assert.Equal("txt", result.Extraction.SourceKind);
            Assert.Equal("plain_text", result.Extraction.ExtractionMethod);
            Assert.Contains("Nicolas Portie", result.ExtractedText);
            Assert.False(result.Extraction.RequiresOcr);
            Assert.True(result.Extraction.TextQualityScore > 0);
            Assert.True(result.Extraction.WordCount > 20);
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public void ReadResume_ArquivoAusente_MarcaRevisaoEOcr()
    {
        var service = new ResumeTextService();

        var result = service.ReadResume(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf"));

        Assert.Equal("", result.Text);
        Assert.Equal("unreadable", result.Report.Classification);
        Assert.True(result.Report.RequiresOcr);
        Assert.True(result.Report.RequiresReview);
        Assert.Contains(result.Report.Warnings, warning => warning.Contains("não encontrado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_OcrMelhoraTexto_PersisteRelatorioEfetivo()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var resumePath = Path.Combine(Path.GetTempPath(), $"jobpilot-scanned-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(resumePath, "arquivo pdf invalido para forcar OCR");

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumePath = resumePath
        });
        await db.SaveChangesAsync();

        var ocrText = string.Join(" ", Enumerable.Repeat("experiencia com ASP.NET Core React SQL automacao gestao tecnica", 20));

        try
        {
            var service = new ProfileImportService(
                db,
                new ResumeTextService(),
                new ResumeOcrService(),
                new FakeAiChatService(),
                new HttpClient(),
                _ => new ResumeOcrResult(ocrText, 160, [], true));

            var result = await service.ImportAsync(userId, new ProfileImportRequest(FetchPublicLinks: false, UseAi: false));

            Assert.Equal("pdf", result.Extraction.SourceKind);
            Assert.Contains("+ocr", result.Extraction.ExtractionMethod);
            Assert.False(result.Extraction.RequiresOcr);
            Assert.False(result.Extraction.RequiresReview);
            Assert.True(result.Extraction.WordCount > 100);
            Assert.Contains("OCR executado", string.Join(" ", result.Warnings));

            var import = Assert.Single(await db.ResumeImports.Where(x => x.UserId == userId).ToListAsync());
            Assert.Equal(result.Extraction.ExtractionMethod, import.ExtractionMethod);
            Assert.Equal(result.Extraction.WordCount, import.WordCount);
            Assert.False(import.RequiresOcr);

            var version = Assert.Single(await db.ResumeVersions.Where(x => x.UserId == userId).ToListAsync());
            var report = JsonSerializer.Deserialize<ResumeExtractionReport>(version.AtsReportJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(report);
            Assert.Equal(result.Extraction.ExtractionMethod, report.ExtractionMethod);
            Assert.Equal(result.Extraction.WordCount, report.WordCount);
            Assert.Equal(result.Extraction.TextQualityScore, version.AtsScore);
        }
        finally
        {
            if (File.Exists(resumePath)) File.Delete(resumePath);
        }
    }

    [Fact]
    public async Task ImportUploadAsync_SalvaArquivoDoNavegadorEImportaBiblioteca()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var uploadRoot = Path.Combine(Path.GetTempPath(), $"jobpilot-upload-test-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Candidate:ResumeUploadDirectory"] = uploadRoot
            })
            .Build();

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("""
            Nicolas Portie
            Email: nicolas@example.com
            Telefone: (11) 99999-9999
            Experiencia com ASP.NET Core, React, TypeScript, SQL, Docker e PostgreSQL.
            Projetos de APIs REST, dashboards e automacao de candidaturas.
            """));
        var file = new FormFile(content, 0, content.Length, "file", "Curriculo Nicolas 2026.txt");
        var importer = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), new HttpClient());
        var service = new ResumeFileService(configuration, importer);

        try
        {
            var result = await service.ImportUploadAsync(userId, file, fetchPublicLinks: false, useAi: false);

            Assert.Equal("txt", result.Extraction.SourceKind);
            Assert.Contains("Nicolas Portie", result.ExtractedText);
            Assert.False(result.Extraction.RequiresOcr);
            var storedProfile = await db.CandidateProfiles.SingleAsync(x => x.UserId == userId);
            Assert.True(File.Exists(storedProfile.ResumePath));
            Assert.Contains(userId.ToString("N"), storedProfile.ResumePath);
            Assert.EndsWith(".txt", storedProfile.ResumePath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" ", Path.GetFileName(storedProfile.ResumePath));

            var import = Assert.Single(await db.ResumeImports.Where(x => x.UserId == userId).ToListAsync());
            Assert.Equal(storedProfile.ResumePath, import.SourcePath);
            Assert.Equal(Path.GetFileName(storedProfile.ResumePath), import.SourceFileName);
            Assert.Single(await db.ResumeDocuments.Where(x => x.UserId == userId).ToListAsync());
            Assert.Contains("ASP.NET Core", result.Profile.Skills);
            Assert.Contains("TypeScript", result.Profile.Skills);
        }
        finally
        {
            if (Directory.Exists(uploadRoot)) Directory.Delete(uploadRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ImportUploadAsync_RejeitaFormatoNaoSuportado()
    {
        await using var db = CreateDb();
        var configuration = new ConfigurationBuilder().Build();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("conteudo"));
        var file = new FormFile(content, 0, content.Length, "file", "curriculo.xlsx");
        var importer = new ProfileImportService(db, new ResumeTextService(), new ResumeOcrService(), new FakeAiChatService(), new HttpClient());
        var service = new ResumeFileService(configuration, importer);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportUploadAsync(Guid.NewGuid(), file, fetchPublicLinks: false, useAi: false));

        Assert.Contains("Formato não suportado", error.Message);
    }


    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        public Task<AnswerDraftResponse> DraftAnswerAsync(Guid userId, string questionText, string? jobContext, CandidateProfile? profile) =>
            Task.FromResult(new AnswerDraftResponse("", false, "fake"));

        public Task<BrowserActionPlanResponse> PlanBrowserActionAsync(Guid userId, BrowserActionPlanRequest request, CandidateProfile? profile, List<ReusableAnswer> answers) =>
            Task.FromResult(new BrowserActionPlanResponse("checkpoint", "", "", "fake", 0m, true));

        public Task<JobFitReviewResponse> ReviewJobFitAsync(Guid userId, JobLead lead, CandidateProfile? profile, JobFilter filter) =>
            Task.FromResult(SearchDecisionHarness.UnavailableReview("fake"));

        public Task<ResumeProfileImportData?> AnalyzeResumeProfileAsync(Guid userId, string resumeText, string publicLinksText, CandidateProfile? existingProfile) =>
            Task.FromResult<ResumeProfileImportData?>(null);

        public Task<JobRequirementExtractionResponse?> ExtractJobRequirementsAsync(Guid userId, JobRequirementExtractionInput input) =>
            Task.FromResult<JobRequirementExtractionResponse?>(null);

        public Task<TailoredResumeGenerationResponse?> GenerateTailoredResumeAsync(Guid userId, TailoredResumeGenerationInput input) =>
            Task.FromResult<TailoredResumeGenerationResponse?>(null);

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            Task.FromResult(new PageStateResponse("unknown", "fake", 0m));

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            Task.FromResult(new StepValidationResponse(false, "fake", 0m, true));

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            Task.FromResult(new AnswerDraftResponse("fake", false, "fake"));
    }

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
