using System.Net;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
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
        var service = CreateService(db, ai);

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "manual_text",
            """
            Acme busca Desenvolvedor .NET Junior remoto.
            Requisitos: Experiencia com ASP.NET Core, APIs REST e SQL.
            Diferencial: Conhecimento em React.
            """,
            "",
            UseAi: true));

        Assert.Equal("Desenvolvedor .NET", result.Title);
        Assert.Equal("Acme", result.Company);
        Assert.Equal("junior", result.Seniority);
        Assert.Equal("remote", result.Workplace);
        Assert.Equal("organized", result.Status);
        Assert.True(result.Requirements.Length >= 2);
        Assert.Contains(result.Requirements, x => x.Label == "ASP.NET Core" && x.Importance == "must_have");
        Assert.Contains(result.Requirements, x => x.Label == "SQL");
        Assert.Single(await db.JobTargets.Where(x => x.UserId == userId).ToListAsync());
        Assert.True(await db.JobRequirements.CountAsync(x => x.UserId == userId) >= 2);
    }

    [Fact]
    public async Task OrganizeAsync_SemIa_UsaFallbackDeterministico()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db, new FakeAiChatService(null));

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
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
    public async Task OrganizeAsync_LimpaTituloProfissionalSemMisturarModeloDeTrabalho()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "desenvolvedor .NET junior home office",
            "",
            "",
            "junior",
            "remote",
            "Vaga para desenvolvedor .NET junior home office.",
            [new JobRequirementItem("technology", ".NET", "must_have", ".NET", ".net", [".NET"])],
            [],
            0.8m));
        var service = CreateService(db, ai);

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "manual_text",
            "Vaga para desenvolvedor .NET junior home office com APIs REST, manutencao de sistemas corporativos e integracoes.",
            "",
            UseAi: true));

        Assert.Equal("Desenvolvedor .NET", result.Title);
        Assert.Equal("remote", result.Workplace);
        Assert.DoesNotContain("home office", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("junior", result.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrganizeAsync_LimpaPrefixoTituloEUsaPlComoPleno()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "Titulo: Desenvolvedor Back-end PL .NET",
            "nstech",
            "Brasil",
            "intern",
            "remote",
            "Vaga para desenvolvedor back-end pleno com .NET.",
            [new JobRequirementItem("technology", ".NET", "must_have", ".NET", ".net", [".NET"])],
            [],
            0.9m));
        var service = CreateService(db, ai);

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "manual_text",
            """
            Titulo: Desenvolvedor Back-end PL .NET
            Empresa: nstech
            A vaga pede C#, .NET, OpenAPI, Kafka, Docker e Azure.
            """,
            "",
            UseAi: true));

        Assert.Equal("Desenvolvedor Back-end .NET", result.Title);
        Assert.Equal("mid", result.Seniority);
        Assert.DoesNotContain("Titulo:", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PL", result.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrganizeAsync_NaoCortaDotNetNoTrechoDeEvidencia()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db, new FakeAiChatService(null));

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "manual_text",
            """
            Desenvolvedor Back-end .NET
            Empresa: nstech
            Descricao completa da vaga: atuar com C#, .NET, OpenAPI, Kafka, Docker, Azure e desenvolvimento de APIs corporativas.
            """,
            "",
            UseAi: false));

        var dotNetRequirement = Assert.Single(result.Requirements, item => item.Label == ".NET");
        Assert.Contains(".NET", dotNetRequirement.EvidenceText);
    }

    [Fact]
    public async Task OrganizeAsync_LimpaSenioridadeDeTituloEmQualquerArea()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "Analista de Dados Senior Remoto",
            "",
            "",
            "senior",
            "remote",
            "Vaga para analista de dados senior remoto com SQL e dashboards.",
            [new JobRequirementItem("technology", "SQL", "must_have", "SQL", "sql", ["SQL"])],
            [],
            0.8m));
        var service = CreateService(db, ai);

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "manual_text",
            "Vaga para analista de dados senior remoto com SQL, dashboards, indicadores e rotinas de relatorios.",
            "",
            UseAi: true));

        Assert.Equal("Analista de Dados", result.Title);
        Assert.Equal("senior", result.Seniority);
        Assert.Equal("remote", result.Workplace);
    }

    [Fact]
    public async Task OrganizeAsync_LinkedInUrl_ExtraiVagaPelaWebAntesDeOrganizar()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var requestedUrls = new List<string>();
        var html = """
        <section>
          <h2 class="top-card-layout__title">Desenvolvedor(a) Full Stack .Net + Angular Pleno</h2>
          <a class="topcard__org-name-link">FMX Solucoes</a>
          <span class="topcard__flavor topcard__flavor--bullet">Agudos, Sao Paulo, Brasil</span>
          <div class="show-more-less-html__markup">
            <strong>Desenvolvedor(a) Full Stack .NET + Angular Pleno</strong><br>
            <strong>PJ | Remoto</strong><br>
            Estamos em busca de uma pessoa Desenvolvedora Full Stack .NET + Angular Pleno para atuar na evolucao de solucoes corporativas.
            <ul>
              <li>Desenvolvimento e manutencao de aplicacoes .NET Core e Angular.</li>
              <li>Construcao e manutencao de Web APIs REST.</li>
              <li>Orientacao a objetos, injecao de dependencia e RabbitMQ.</li>
              <li>SQL Server, MongoDB, Docker, Kubernetes, Azure DevOps e CI/CD.</li>
              <li>Arquitetura distribuida, performance, escalabilidade e integracoes entre sistemas.</li>
            </ul>
          </div>
          <ul class="description__job-criteria-list">
            <li class="description__job-criteria-item"><h3>Seniority level</h3><span class="description__job-criteria-text">Mid-Senior level</span></li>
            <li class="description__job-criteria-item"><h3>Employment type</h3><span class="description__job-criteria-text">Contract</span></li>
          </ul>
        </section>
        """;
        var ai = new FakeAiChatService(new JobRequirementExtractionResponse(
            "Desenvolvedor(a) Full Stack .Net + Angular Pleno",
            "FMX Solucoes",
            "Agudos, Sao Paulo, Brasil",
            "mid",
            "remote",
            "Vaga full stack para .NET Core, Angular, mensageria, cloud/devops e arquitetura distribuida.",
            [
                new JobRequirementItem("technology", ".NET Core", "must_have", "aplicacoes .NET Core e Angular", "dotnet-core", [".NET Core"]),
                new JobRequirementItem("technology", "Angular", "must_have", "aplicacoes .NET Core e Angular", "angular", ["Angular"]),
                new JobRequirementItem("technology", "RabbitMQ", "must_have", "injecao de dependencia, RabbitMQ, SQL Server", "rabbitmq", ["RabbitMQ"]),
                new JobRequirementItem("technology", "Docker", "must_have", "Docker, Kubernetes, Azure DevOps", "docker", ["Docker"])
            ],
            [],
            0.9m));
        var service = CreateService(db, ai, html, requestedUrls);

        var result = await service.OrganizeAsync(userId, new OrganizeJobTargetRequest(
            "url",
            "Observacao manual: disponibilidade para contrato PJ.",
            "https://www.linkedin.com/jobs/search/?currentJobId=4424940622&keywords=desenvolvedor%20.NET%20junior%20home%20office",
            UseAi: true));

        Assert.Equal("url", result.SourceKind);
        Assert.Equal("https://www.linkedin.com/jobs/view/4424940622/", result.SourceUrl);
        Assert.Equal("FMX Solucoes", result.Company);
        Assert.Equal("Desenvolvedor Full Stack .NET + Angular", result.Title);
        Assert.Equal("mid", result.Seniority);
        Assert.Equal("remote", result.Workplace);
        Assert.DoesNotContain("Pleno", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Requirements, x => x.Label == "Angular");
        Assert.Contains(result.Requirements, x => x.Label == "RabbitMQ");
        Assert.Contains(result.Requirements, x => x.Label == "MongoDB");
        Assert.Contains(result.Requirements, x => x.Label == "SQL Server");
        Assert.Contains(result.Requirements, x => x.Label == "Azure DevOps");
        Assert.Contains(result.Requirements, x => x.Label == "CI/CD");
        Assert.Contains(requestedUrls, url => url == "https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/4424940622");
        Assert.NotNull(ai.LastExtractionInput);
        Assert.Contains("RabbitMQ", ai.LastExtractionInput!.SourceText);
        Assert.Contains("Seniority level: Mid-Senior level", ai.LastExtractionInput.SourceText);
        Assert.Contains("Observacoes manuais do usuario", ai.LastExtractionInput.SourceText);
    }

    private static JobPilotDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<JobPilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new JobPilotDbContext(options);
    }

    private static JobTargetService CreateService(
        JobPilotDbContext db,
        IAiChatService ai,
        string urlHtml = "",
        List<string>? requestedUrls = null)
    {
        var http = new HttpClient(new FakeHttpMessageHandler(urlHtml, requestedUrls));
        return new JobTargetService(db, ai, new ResumeOcrService(), new JobTargetUrlExtractionService(http));
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

    private sealed class FakeHttpMessageHandler(string html, List<string>? requestedUrls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requestedUrls?.Add(request.RequestUri?.ToString() ?? "");
            var content = string.IsNullOrWhiteSpace(html)
                ? "<html><body>Vaga generica para testes de servico sem acesso externo.</body></html>"
                : html;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
