using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Tests;

public class ResumeTailoringServiceTests
{
    [Fact]
    public async Task BuildGenericAtsAsync_CriaCurriculoAtsSemVaga()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            Email = "nicolas@example.com",
            Phone = "(11) 99999-9999",
            City = "Sao Paulo",
            Country = "Brazil",
            TargetRole = "Desenvolvedor .NET",
            ProfessionalSummary = "Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL.",
            Skills = ["C#", ".NET", "ASP.NET Core", "SQL"]
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Curriculo base", Status = "ready" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            TargetRole = "Desenvolvedor .NET",
            PlainText = "Nicolas Portie\nExperiencia com C#, ASP.NET Core, APIs REST e SQL.",
            Sections =
            [
                Section(userId, versionId, "summary", "Resumo", 20, new { text = "Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL." }),
                Section(userId, versionId, "skills", "Competencias", 30, new { items = new[] { "C#", ".NET", "ASP.NET Core", "SQL" } }),
                Section(userId, versionId, "experience", "Experiencia", 40, new { items = new[] { new { company = "Acme", role = "Desenvolvedor", period = "2024 - atual", description = "Desenvolvimento de APIs REST com ASP.NET Core e SQL." } } }),
                Section(userId, versionId, "education", "Educacao", 60, new { items = new[] { new { institution = "Faculdade X", degree = "Analise e Desenvolvimento de Sistemas", year = "2025" } } })
            ]
        });
        await db.SaveChangesAsync();

        var service = new ResumeTailoringService(db, new ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.BuildGenericAtsAsync(userId, new BuildGenericAtsResumeRequest(versionId, "ats_reverse_chronological"));

        Assert.NotEqual(Guid.Empty, result.TailoredVersionId);
        Assert.Equal(Guid.Empty, result.JobTargetId);
        Assert.Equal(Guid.Empty, result.EvidenceMapRunId);
        Assert.Contains("RESUMO PROFISSIONAL", result.PlainText);
        Assert.Contains("COMPETENCIAS", result.PlainText);
        Assert.Contains("EXPERIENCIA PROFISSIONAL", result.PlainText);
        Assert.Contains("FORMACAO", result.PlainText);
        Assert.Contains("ASP.NET Core", result.PlainText);
        Assert.DoesNotContain("PENDENCIAS PARA REVISAO", result.PlainText);
        Assert.Contains(result.ScoreComponents, x => x.Key == "structure");
        Assert.Contains("Curriculo ATS", result.Version.Title);
        Assert.Equal(2, await db.ResumeVersions.CountAsync(x => x.UserId == userId));
        Assert.Empty(await db.ResumeEvidenceMapRuns.Where(x => x.UserId == userId).ToListAsync());
        Assert.Single(await db.ResumeTailoringRuns.Where(x => x.UserId == userId && x.PromptVersion == "generic-ats-resume-v1").ToListAsync());
    }

    [Fact]
    public void LayoutOptions_IncluiModelosAtsSeguros()
    {
        var keys = ResumeTailoringService.LayoutOptions.Select(x => x.Key).ToArray();

        Assert.Contains("ats_reverse_chronological", keys);
        Assert.Contains("ats_classic", keys);
        Assert.Contains("ats_compact", keys);
        Assert.Contains("ats_skills_first", keys);
        Assert.Contains("ats_modern_minimal", keys);
        Assert.Contains("human_review", keys);
        Assert.All(ResumeTailoringService.LayoutOptions.Where(x => x.RecommendedForAts), option =>
        {
            Assert.Contains("coluna", option.AtsNotes, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tabelas", option.Description, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task BuildGenericAtsAsync_LimpaTextoImportadoDuplicado()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie Sussai Silva",
            Email = "contato@nicolasportie.com",
            Phone = "(18) 99664-6794",
            City = "Penapolis",
            Country = "Brazil",
            TargetRole = "ASP.NET Developer",
            ProfessionalSummary = "NICOLAS PORTIE Desenvolvedor de Software | C# | .NET | APIs | SQL Penapolis/SP | (18) 99664-6794 | contato@nicolasportie.com | LinkedIn | GitHub | Portfolio PERFIL PROFISSIONAL Desenvolvedor de Software com experiencia em sistemas corporativos, desenvolvimento de aplicacoes, APIs, integracoes e analise de regras de negocio. COMPETENCIAS C#, .NET, ASP.NET Core"
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Curriculo base", Status = "ready" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base importada",
            TargetRole = "ASP.NET Developer",
            PlainText = "Nicolas Portie Sussai Silva contato@nicolasportie.com | (18) 99664-6794 | Penapolis | Brazil OBJETIVO ASP.NET Developer RESUMO NICOLAS PORTIE Desenvolvedor de Software | C# | .NET | APIs | SQL | contato@nicolasportie.com | LinkedIn | GitHub | Portfolio PERFIL PROFISSIONAL Desenvolvedor de Software com experiencia em sistemas corporativos, desenvolvimento de aplicacoes, APIs, integracoes e analise de regras de negocio. COMPETENCIAS C#, .NET, ASP.NET Core, Entity Framework, SQL, PostgreSQL, APIs REST TEXTO IMPORTADO NICOLAS PORTIE Desenvolvedor de Software | contato@nicolasportie.com EXPERIENCIA PROFISSIONAL Irrigacao Penapolis - Programador de Sistemas | Abr/2025 - Atual Desenvolvimento de APIs REST, manutencao de sistemas e analise de regras de negocio. FORMACAO Analise e Desenvolvimento de Sistemas | 2025",
            Sections =
            [
                Section(userId, versionId, "identity", "Identidade", 10, new { fullName = "Nicolas Portie Sussai Silva", email = "contato@nicolasportie.com", phone = "(18) 99664-6794", city = "Penapolis", country = "Brazil" }),
                Section(userId, versionId, "summary", "Resumo", 20, new { text = "NICOLAS PORTIE Desenvolvedor de Software | C# | .NET | APIs | SQL | contato@nicolasportie.com PERFIL PROFISSIONAL Desenvolvedor de Software com experiencia em sistemas corporativos, desenvolvimento de aplicacoes, APIs, integracoes e analise de regras de negocio. COMPETENCIAS C#, .NET" }),
                Section(userId, versionId, "skills", "Competencias", 30, new { items = new[] { "C#", ".NET", "ASP.NET Core", "contato@nicolasportie.com", "Penapolis/SP | (18) 99664-6794" } })
            ]
        });
        await db.SaveChangesAsync();

        var service = new ResumeTailoringService(db, new ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.BuildGenericAtsAsync(userId, new BuildGenericAtsResumeRequest(versionId, "ats_reverse_chronological"));

        Assert.DoesNotContain("CONTEUDO DO CURRICULO BASE", result.PlainText);
        Assert.DoesNotContain("TEXTO IMPORTADO", result.PlainText);
        Assert.DoesNotContain("contato@nicolasportie.com PERFIL PROFISSIONAL", result.PlainText);
        Assert.Contains("RESUMO PROFISSIONAL", result.PlainText);
        Assert.Contains("Desenvolvedor de Software com experiencia", result.PlainText);
        Assert.Contains("EXPERIENCIA PROFISSIONAL", result.PlainText);
        Assert.Contains("FORMACAO", result.PlainText);
        Assert.DoesNotContain("Penapolis/SP | (18) 99664-6794", result.PlainText);
        Assert.Equal(1, CountOccurrences(result.PlainText, "contato@nicolasportie.com"));
    }

    [Fact]
    public async Task TailorAsync_CriaVersaoDirecionadaSemInventarRequisitosSemEvidencia()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Nicolas Portie",
            Email = "nicolas@example.com",
            Phone = "(11) 99999-9999",
            City = "Sao Paulo",
            Country = "Brazil",
            TargetRole = "Desenvolvedor .NET",
            ProfessionalSummary = "Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL.",
            Skills = ["C#", ".NET", "ASP.NET Core", "SQL", "JavaScript"]
        });
        db.ResumeDocuments.Add(new ResumeDocument
        {
            Id = documentId,
            UserId = userId,
            Title = "Curriculo base",
            Status = "ready"
        });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            TargetRole = "Desenvolvedor .NET",
            PlainText = "Experiencia com C#, ASP.NET Core, APIs REST, SQL, JavaScript e dashboards web."
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Desenvolvedor .NET Junior",
            Company = "Acme",
            Workplace = "remote",
            Requirements =
            [
                Req(userId, targetId, "technology", "ASP.NET Core", "must_have", "asp.net-core", 1),
                Req(userId, targetId, "technology", "React", "nice_to_have", "react", 2),
                Req(userId, targetId, "technology", "Kubernetes", "must_have", "kubernetes", 3),
                Req(userId, targetId, "language", "Ingles avancado", "must_have", "ingles-avancado", 4)
            ]
        });
        await db.SaveChangesAsync();

        var service = new ResumeTailoringService(db, new ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));

        Assert.Equal("needs_review", result.Status);
        Assert.Contains("ASP.NET Core", result.IncludedKeywords);
        Assert.Contains("React", result.AdjacentKeywords);
        Assert.Contains("Kubernetes", result.ExcludedKeywords);
        Assert.Contains("Ingles avancado", result.ExcludedKeywords);
        Assert.Contains("COMPETENCIAS COM EVIDENCIA", result.PlainText);
        Assert.Contains("COMPETENCIAS RELACIONADAS", result.PlainText);
        Assert.Contains("PENDENCIAS PARA REVISAO", result.PlainText);
        Assert.DoesNotContain("Kubernetes,", result.PlainText);
        Assert.DoesNotContain("Ingles avancado,", result.PlainText);
        Assert.Equal(5, result.ScoreComponents.Length);
        Assert.Equal(result.AtsScore, result.ScoreComponents.Sum(x => x.Score));
        Assert.Contains(result.ScoreComponents, x => x.Key == "evidence" && x.MaxScore == 45);
        Assert.Contains(result.ScoreComponents, x => x.Key == "coverage" && x.Reason.Contains("requisito", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ScoreComponents, x => x.Key == "readiness" && x.Status is "warn" or "bad");
        Assert.Contains(result.SectionDiffs, x => x.SectionType == "target" && x.Status == "added");
        Assert.Contains(result.SectionDiffs, x => x.SectionType == "skills" && x.Status is "added" or "changed");
        Assert.Contains(result.SectionDiffs, x => x.SectionType == "plain_text" && x.Status == "changed");
        var question = Assert.Single(result.ReviewQuestions);
        Assert.Equal("Ingles avancado", question.RequirementLabel);
        Assert.Equal("language", question.Category);
        Assert.Contains("proficiencia real", question.QuestionText);

        var versions = await db.ResumeVersions.Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(result.TailoredVersionId, versions.Last().Id);
        Assert.Equal("Acme", versions.Last().TargetCompany);
        Assert.Contains("scoreComponents", versions.Last().AtsReportJson);
        Assert.Contains("sectionDiffs", versions.Last().AtsReportJson);
        Assert.Contains("reviewQuestions", versions.Last().AtsReportJson);
        Assert.Single(await db.ResumeTailoringRuns.Where(x => x.UserId == userId).ToListAsync());
        Assert.Single(await db.ResumeEvidenceMapRuns.Where(x => x.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task TailorAsync_ReusaMapaDeEvidenciaInformado()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "C# ASP.NET Core SQL"
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Dev .NET",
            Requirements = [Req(userId, targetId, "technology", "ASP.NET Core", "must_have", "asp.net-core", 1)]
        });
        await db.SaveChangesAsync();

        var evidenceService = new ResumeEvidenceMapService(db);
        var map = await evidenceService.BuildAsync(userId, new BuildResumeEvidenceMapRequest(versionId, targetId));

        var service = new ResumeTailoringService(db, evidenceService, new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, map.Id, UseAi: false));

        Assert.Equal(map.Id, result.EvidenceMapRunId);
        Assert.Equal(1, await db.ResumeEvidenceMapRuns.CountAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task TailorAsync_RejeitaTextoDaIaQuandoIncluiRequisitoSemEvidenciaComoFato()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Experiencia com C#, ASP.NET Core e SQL."
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Dev Cloud",
            Requirements = [Req(userId, targetId, "technology", "Kubernetes", "must_have", "kubernetes", 1)]
        });
        await db.SaveChangesAsync();

        var fakeAi = new FakeAiChatService(new TailoredResumeGenerationResponse(
            """
            Nicolas Portie

            RESUMO PROFISSIONAL
            Experiencia direta com Kubernetes, C# e plataformas cloud.
            """,
            []));
        var service = new ResumeTailoringService(db, new ResumeEvidenceMapService(db), fakeAi);

        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.DoesNotContain("Experiencia direta com Kubernetes", result.PlainText);
        Assert.Contains("PENDENCIAS PARA REVISAO", result.PlainText);
        Assert.Contains("Kubernetes", result.ExcludedKeywords);
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

    private static ResumeSection Section(Guid userId, Guid versionId, string type, string title, int order, object content) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = versionId,
            SectionType = type,
            Title = title,
            SortOrder = order,
            ContentJson = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
        };

    private static int CountOccurrences(string value, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private sealed class FakeAiChatService(TailoredResumeGenerationResponse? tailoredResponse = null) : IAiChatService
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
            Task.FromResult(tailoredResponse);

        public Task<PageStateResponse> ClassifyPageStateAsync(Guid userId, PageStateRequest request) =>
            Task.FromResult(new PageStateResponse("unknown", "fake", 0m));

        public Task<StepValidationResponse> ValidateApplicationStepAsync(Guid userId, StepValidationRequest request) =>
            Task.FromResult(new StepValidationResponse(false, "fake", 0m, true));

        public Task<AnswerDraftResponse> TestProviderAsync(Guid userId, Guid providerId) =>
            Task.FromResult(new AnswerDraftResponse("fake", false, "fake"));
    }
}
