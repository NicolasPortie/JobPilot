using System.Globalization;
using System.Text;
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

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.BuildGenericAtsAsync(userId, new BuildGenericAtsResumeRequest(versionId, "ats_reverse_chronological"));

        Assert.NotEqual(Guid.Empty, result.TailoredVersionId);
        Assert.Equal(Guid.Empty, result.JobTargetId);
        Assert.Equal(Guid.Empty, result.EvidenceMapRunId);
        Assert.Contains("RESUMO PROFISSIONAL", result.PlainText);
        AssertContainsIgnoringDiacritics("COMPETENCIAS", result.PlainText);
        AssertContainsIgnoringDiacritics("EXPERIENCIA PROFISSIONAL", result.PlainText);
        AssertContainsIgnoringDiacritics("FORMACAO", result.PlainText);
        Assert.Contains("ASP.NET Core", result.PlainText);
        Assert.DoesNotContain("com uso de", result.PlainText, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("ats_standard", keys);
        Assert.Contains("ats_compact", keys);
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

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.BuildGenericAtsAsync(userId, new BuildGenericAtsResumeRequest(versionId, "ats_reverse_chronological"));

        Assert.DoesNotContain("CONTEUDO DO CURRICULO BASE", result.PlainText);
        Assert.DoesNotContain("TEXTO IMPORTADO", result.PlainText);
        Assert.DoesNotContain("contato@nicolasportie.com PERFIL PROFISSIONAL", result.PlainText);
        Assert.Contains("RESUMO PROFISSIONAL", result.PlainText);
        AssertContainsIgnoringDiacritics("Desenvolvedor de Software com experiencia", result.PlainText);
        Assert.DoesNotContain("com uso de", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C#, .NET, APIs", ExtractSummary(result.PlainText), StringComparison.OrdinalIgnoreCase);
        AssertContainsIgnoringDiacritics("EXPERIENCIA PROFISSIONAL", result.PlainText);
        AssertContainsIgnoringDiacritics("FORMACAO", result.PlainText);
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
            PlainText = "Experiencia com C#, ASP.NET Core, APIs REST, SQL, JavaScript e dashboards web.",
            Sections =
            [
                Section(userId, versionId, "projects", "Projetos", 50, new { items = new[] { new { name = "Acesso Veicular", description = "Sistema desenvolvido com .\nSistema web com .NET e SQL para controle de entrada." } } })
            ]
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "desenvolvedor .NET junior home office",
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

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));

        Assert.Equal("needs_review", result.Status);
        Assert.Contains("ASP.NET Core", result.IncludedKeywords);
        Assert.Contains("React", result.AdjacentKeywords);
        Assert.Contains("Kubernetes", result.ExcludedKeywords);
        Assert.Contains("Ingles avancado", result.ExcludedKeywords);
        Assert.Contains("Desenvolvedor .NET", result.PlainText);
        Assert.DoesNotContain("Desenvolvedor .NET Junior", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" junior", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("home office", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sistema desenvolvido com .", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sistema web com .NET e SQL", result.PlainText);
        Assert.DoesNotContain("OBJETIVO", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Desenvolvedor .NET", result.Version.TargetRole);
        AssertContainsIgnoringDiacritics("COMPETENCIAS", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("COMPETENCIAS COM EVIDENCIA", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("COMPETENCIAS RELACIONADAS", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("EVIDENCIAS RELEVANTES", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("HISTORICO BASE", result.PlainText);
        Assert.DoesNotContain("Evidências diretas para esta vaga", ExtractSummary(result.PlainText), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PENDENCIAS PARA REVISAO", result.PlainText);
        Assert.NotEmpty(result.Checkpoints);
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
    public async Task TailorAsync_RemoveEmpresaDaVagaDoHeadlineQuandoIaInclui()
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
            TargetRole = "Desenvolvedor Back-end .NET",
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
            TargetRole = "Desenvolvedor Back-end .NET",
            PlainText = "Nicolas Portie\nDesenvolvedor Back-end .NET\nExperiencia com C#, ASP.NET Core, APIs REST e SQL.",
            Sections =
            [
                Section(userId, versionId, "summary", "Resumo", 20, new { text = "Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL." }),
                Section(userId, versionId, "skills", "Competencias", 30, new { items = new[] { "C#", ".NET", "ASP.NET Core", "SQL" } })
            ]
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Desenvolvedor Back-end .NET | nstech",
            Company = "nstech",
            Requirements =
            [
                Req(userId, targetId, "technology", "ASP.NET Core", "must_have", "asp.net-core", 1)
            ]
        });
        await db.SaveChangesAsync();

        var aiPlainText = """
            Nicolas Portie
            Desenvolvedor Back-end .NET | nstech
            contato@nicolas@example.com | (11) 99999-9999

            RESUMO PROFISSIONAL
            Desenvolvedor com experiencia em APIs REST, ASP.NET Core e SQL.

            COMPETENCIAS
            C#, .NET, ASP.NET Core, SQL
            """;
        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(new TailoredResumeGenerationResponse(aiPlainText, [])));

        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.Contains("Desenvolvedor Back-end", result.PlainText);
        Assert.DoesNotContain("Desenvolvedor Back-end .NET", HeaderBeforeFirstSection(result.PlainText), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Desenvolvedor Back-end .NET | nstech", result.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nstech", HeaderBeforeFirstSection(result.PlainText), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Desenvolvedor Back-end", result.Version.TargetRole);
        Assert.Equal("Curriculo direcionado - Desenvolvedor Back-end - nstech", result.Version.Title);
    }

    [Fact]
    public async Task TailorAsync_UsaHeadlineGenericaSemSenioridadeOuStack()
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
            TargetRole = "Desenvolvedor Back-end .NET",
            ProfessionalSummary = "Desenvolvedor com experiencia em APIs REST.",
            Skills = ["C#", ".NET", "ASP.NET Core"]
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Curriculo base", Status = "ready" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            TargetRole = "Desenvolvedor Back-end .NET",
            PlainText = "Nicolas Portie\nDesenvolvedor Back-end .NET\nExperiencia com C#, ASP.NET Core e APIs REST."
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Desenvolvedor Back-end PL .NET",
            Company = "Acme",
            Requirements = [Req(userId, targetId, "technology", "ASP.NET Core", "must_have", "asp.net-core", 1)]
        });
        await db.SaveChangesAsync();

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(null));

        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));
        var header = HeaderBeforeFirstSection(result.PlainText);

        Assert.Contains("Desenvolvedor Back-end", header);
        Assert.DoesNotContain("PL", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".NET", header, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Desenvolvedor Back-end", result.Version.TargetRole);
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

        var evidenceService = TestServices.ResumeEvidenceMapService(db);
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
        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), fakeAi);

        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.DoesNotContain("Experiencia direta com Kubernetes", result.PlainText);
        Assert.DoesNotContain("PENDENCIAS PARA REVISAO", result.PlainText);
        Assert.NotEmpty(result.Checkpoints);
        Assert.Contains("Kubernetes", result.ExcludedKeywords);
    }

    private async Task<(JobPilotDbContext Db, Guid UserId, Guid VersionId, Guid TargetId)> SeedTruthScenarioAsync(string summary = "", string targetRole = "")
    {
        var db = CreateDb();
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        db.CandidateProfiles.Add(new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Ana Souza",
            Email = "ana@example.com",
            Phone = "(11) 98888-7777",
            City = "Campinas",
            State = "SP",
            TargetRole = targetRole,
            ProfessionalSummary = summary,
            Skills = []
        });
        db.ResumeDocuments.Add(new ResumeDocument { Id = documentId, UserId = userId, Title = "Base", Status = "ready" });
        db.ResumeVersions.Add(new ResumeVersion
        {
            Id = versionId,
            UserId = userId,
            ResumeDocumentId = documentId,
            Title = "Base",
            PlainText = "Ana Souza",
            Sections =
            [
                Section(userId, versionId, "identity", "Identidade", 10, new { fullName = "Ana Souza", headline = "Analista de Dados" }),
                Section(userId, versionId, "skills", "Competencias", 30, new { items = new[] { "Excel", "SQL", "Power BI", "Docker" } }),
                Section(userId, versionId, "experience", "Experiencia", 40, new
                {
                    items = new[]
                    {
                        new { company = "Acme", role = "Analista", period = "2020 - 2023", description = "Organizou o arquivo morto.\nCriou consultas em SQL para relatórios mensais.\nUsou Docker para publicar o painel de indicadores." }
                    }
                }),
                Section(userId, versionId, "projects", "Projetos", 50, new
                {
                    items = new[]
                    {
                        new { name = "Horta Comunitária", description = "Calendário de plantio." },
                        new { name = "Painel de Vendas", description = "Dashboard em Power BI com SQL." }
                    }
                })
            ]
        });
        db.JobTargets.Add(new JobTarget
        {
            Id = targetId,
            UserId = userId,
            Title = "Analista de BI",
            Company = "Beta",
            Requirements =
            [
                Req(userId, targetId, "technology", "SQL", "must_have", "sql", 1),
                Req(userId, targetId, "technology", "Docker", "nice_to_have", "docker", 2),
                Req(userId, targetId, "technology", "Power BI", "must_have", "power-bi", 3),
                Req(userId, targetId, "technology", "Kubernetes", "must_have", "kubernetes", 4)
            ]
        });
        await db.SaveChangesAsync();
        return (db, userId, versionId, targetId);
    }

    [Fact]
    public async Task TailorAsync_NaoInventaResumoQuandoABaseNaoTem()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync(summary: "", targetRole: "Engenheiro Kubernetes");
        await using var _ = db;

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));

        Assert.DoesNotContain("RESUMO PROFISSIONAL", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("sistemas corporativos", result.PlainText);
        AssertDoesNotContainIgnoringDiacritics("desenvolvimento de software", result.PlainText);
        // O cargo desejado é intenção: Kubernetes não pode virar competência comprovada.
        Assert.DoesNotContain("Kubernetes", result.IncludedKeywords);
        Assert.DoesNotContain("Kubernetes", result.PlainText);
    }

    [Fact]
    public async Task TailorAsync_PriorizaCompetenciasEBulletsDaVaga()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));
        var text = result.PlainText;

        var skillsLine = text.Split('\n').Single(line => line.StartsWith("SQL, "));
        Assert.True(skillsLine.IndexOf("SQL", StringComparison.Ordinal) < skillsLine.IndexOf("Excel", StringComparison.Ordinal));
        Assert.True(skillsLine.IndexOf("Power BI", StringComparison.Ordinal) < skillsLine.IndexOf("Excel", StringComparison.Ordinal));

        // Bullet com termos da vaga sobe; o irrelevante desce. Projeto relevante vem antes do outro.
        Assert.True(text.IndexOf("Criou consultas em SQL", StringComparison.Ordinal) < text.IndexOf("Organizou o arquivo morto", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Painel de Vendas", StringComparison.Ordinal) < text.IndexOf("Horta Comunitária", StringComparison.Ordinal));
        Assert.Contains("Analista - Acme | 2020 - 2023", text);
        Assert.DoesNotContain("Kubernetes", text);
    }

    [Fact]
    public async Task EditarSecaoDaVersaoAdaptada_RegeneraTextoSemDuplicarNemVazarMetadados()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;
        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));
        var experience = result.Version.Sections.Single(x => x.SectionType == "experience");

        var sections = TestServices.ResumeSectionService(db);
        await sections.UpdateAsync(userId, experience.Id, new UpdateResumeSectionRequest(
            ContentJson: "{\"items\":[{\"company\":\"Acme\",\"role\":\"Analista Sênior\",\"period\":\"2020 - 2023\",\"description\":\"Liderou o time de dados.\"}]}"));
        db.ChangeTracker.Clear();
        var saved = await db.ResumeVersions.Include(x => x.Sections).SingleAsync(x => x.Id == result.TailoredVersionId);

        Assert.Contains("Analista Sênior - Acme", saved.PlainText);
        Assert.Contains("Liderou o time de dados.", saved.PlainText);
        Assert.DoesNotContain("Organizou o arquivo morto", saved.PlainText);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(saved.PlainText, "EXPERIÊNCIA PROFISSIONAL"));
        Assert.DoesNotContain("Vaga alvo", saved.PlainText);
        Assert.DoesNotContain("Beta", saved.PlainText);
        Assert.DoesNotContain("Kubernetes", saved.PlainText);
        Assert.Equal(saved.PlainText, System.Text.Json.JsonDocument.Parse(saved.Sections.Single(x => x.SectionType == "plain_text").ContentJson).RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task TailorAsync_RejeitaTextoDaIaQueCitaTecnologiaForaDoCurriculo()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;
        var invented = new TailoredResumeGenerationResponse(
            "Ana Souza\nAnalista de BI\n\nRESUMO PROFISSIONAL\nAnalista com experiência em SQL, Power BI e também em Terraform e Ansible para infraestrutura.\n\nCOMPETÊNCIAS\nSQL, Power BI, Terraform, Ansible\n\nEXPERIÊNCIA PROFISSIONAL\nAnalista - Acme | 2020 - 2023\n- Criou consultas em SQL.",
            []);

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(invented));
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.DoesNotContain("Terraform", result.PlainText);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public async Task TailorAsync_AvisaQuandoOTextoDaIaFoiDescartado()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;
        var invented = new TailoredResumeGenerationResponse(
            "Ana Souza\nana@example.com\nAnalista de BI\n\nRESUMO PROFISSIONAL\nAnalista com experiência em SQL, Power BI e também em Terraform e Ansible para infraestrutura.\n\nCOMPETÊNCIAS\nSQL, Power BI, Terraform, Ansible\n\nEXPERIÊNCIA PROFISSIONAL\nAnalista - Acme | 2020 - 2023\n- Criou consultas em SQL.",
            []);

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(invented));
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.False(result.UsedAi);
        Assert.NotNull(result.AiNotice);
        Assert.Contains("descartado", result.AiNotice);
        Assert.Contains("Terraform", result.AiNotice);
        Assert.Contains(result.AiNotice, result.Warnings);
    }

    [Fact]
    public async Task TailorAsync_AvisaQuandoAIaNaoResponde()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(null));
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.False(result.UsedAi);
        Assert.Contains("não respondeu", result.AiNotice);
    }

    [Fact]
    public async Task TailorAsync_SemIaPedida_NaoGeraAviso()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(null));
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));

        Assert.Null(result.AiNotice);
    }

    [Fact]
    public async Task TailorAsync_DescartaTextoDaIaSemOEmailDoCabecalho()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;
        var semContato = new TailoredResumeGenerationResponse(
            "Ana Souza\nAnalista de BI\n\nRESUMO PROFISSIONAL\nAnalista com experiência em SQL e Power BI, criando consultas e painéis de indicadores.\n\nCOMPETÊNCIAS\nSQL, Power BI\n\nEXPERIÊNCIA PROFISSIONAL\nAnalista - Acme | 2020 - 2023\n- Criou consultas em SQL.",
            []);

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService(semContato));
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: true));

        Assert.False(result.UsedAi);
        Assert.Contains("e-mail", result.AiNotice);
        Assert.Contains("ana@example.com", result.PlainText);
    }

    [Fact]
    public async Task TailorAsync_ReconheceApelidoDeTecnologiaComoEvidencia()
    {
        var (db, userId, versionId, targetId) = await SeedTruthScenarioAsync();
        await using var _ = db;
        db.JobRequirements.Add(Req(userId, targetId, "technology", "Entity Framework Core", "must_have", "entity-framework-core", 9));
        var skills = db.ResumeSections.Single(x => x.ResumeVersionId == versionId && x.SectionType == "skills");
        skills.ContentJson = "{\"items\":[\"SQL\",\"EF Core\"]}";
        await db.SaveChangesAsync();

        var service = new ResumeTailoringService(db, TestServices.ResumeEvidenceMapService(db), new FakeAiChatService());
        var result = await service.TailorAsync(userId, new TailorResumeRequest(versionId, targetId, UseAi: false));

        Assert.Contains("Entity Framework Core", result.IncludedKeywords);
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

    private static string ExtractSummary(string plainText)
    {
        var start = plainText.IndexOf("RESUMO PROFISSIONAL", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";
        start += "RESUMO PROFISSIONAL".Length;
        var next = plainText.IndexOf("\nCOMPET", start, StringComparison.OrdinalIgnoreCase);
        if (next < 0) next = plainText.IndexOf("\r\nCOMPET", start, StringComparison.OrdinalIgnoreCase);
        if (next < 0) next = plainText.Length;
        return plainText[start..next];
    }

    private static string HeaderBeforeFirstSection(string plainText)
    {
        var start = plainText.IndexOf("RESUMO PROFISSIONAL", StringComparison.OrdinalIgnoreCase);
        if (start < 0) start = plainText.IndexOf("COMPETENCIAS", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return plainText;
        return plainText[..start];
    }

    private static void AssertContainsIgnoringDiacritics(string expected, string actual) =>
        Assert.Contains(RemoveDiacritics(expected), RemoveDiacritics(actual), StringComparison.OrdinalIgnoreCase);

    private static void AssertDoesNotContainIgnoringDiacritics(string expected, string actual) =>
        Assert.DoesNotContain(RemoveDiacritics(expected), RemoveDiacritics(actual), StringComparison.OrdinalIgnoreCase);

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
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
