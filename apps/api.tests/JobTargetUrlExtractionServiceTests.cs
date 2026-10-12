using System.Net;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class JobTargetUrlExtractionServiceTests
{
    [Fact]
    public async Task ResolveAsync_LinkedInSearchUrl_BuscaEndpointPublicoDaVaga()
    {
        var requested = new List<string>();
        var service = new JobTargetUrlExtractionService(new HttpClient(new FakeHttpMessageHandler(_ => LinkedInHtml(), requested)));

        var result = await service.ResolveAsync(
            "https://www.linkedin.com/jobs/search/?currentJobId=4424940622&keywords=desenvolvedor%20.NET",
            "Observacao manual.");

        Assert.Equal("url", result.Kind);
        Assert.Equal("https://www.linkedin.com/jobs/view/4424940622/", result.Url);
        Assert.Equal("Desenvolvedor(a) Full Stack .Net + Angular Pleno", result.Title);
        Assert.Equal("FMX Solucoes", result.Company);
        Assert.Equal("Agudos, Sao Paulo, Brasil", result.Location);
        Assert.Contains("https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/4424940622", requested);
        Assert.Contains("RabbitMQ", result.Text);
        Assert.Contains("MongoDB", result.Text);
        Assert.Contains("Azure DevOps", result.Text);
        Assert.Contains("Desenvolvedor Full Stack .NET + Angular", result.Text);
        Assert.DoesNotContain("Titulo:", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Seniority level: Mid-Senior level", result.Text);
        Assert.Contains("Observacoes manuais do usuario", result.Text);
    }

    [Fact]
    public async Task ResolveAsync_LinkedInViewUrl_ExtraiIdDoPath()
    {
        var requested = new List<string>();
        var service = new JobTargetUrlExtractionService(new HttpClient(new FakeHttpMessageHandler(_ => LinkedInHtml(), requested)));

        var result = await service.ResolveAsync("https://www.linkedin.com/jobs/view/4424940622/", "");

        Assert.Equal("https://www.linkedin.com/jobs/view/4424940622/", result.Url);
        Assert.Contains("https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/4424940622", requested);
        Assert.Contains("LinkedIn Job ID: 4424940622", result.Text);
    }

    [Fact]
    public async Task ResolveAsync_JsonLdJobPosting_ExtraiDadosEstruturados()
    {
        var service = new JobTargetUrlExtractionService(new HttpClient(new FakeHttpMessageHandler(_ => """
        <html>
          <head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@type": "JobPosting",
              "title": "Analista de Dados",
              "description": "Atuar com SQL, Python e dashboards para indicadores de negocio.",
              "employmentType": "FULL_TIME",
              "skills": ["SQL", "Python", "Power BI"],
              "hiringOrganization": { "@type": "Organization", "name": "DataCo" },
              "jobLocation": {
                "@type": "Place",
                "address": {
                  "@type": "PostalAddress",
                  "addressLocality": "Sao Paulo",
                  "addressRegion": "SP",
                  "addressCountry": "BR"
                }
              }
            }
            </script>
          </head>
        </html>
        """)));

        var result = await service.ResolveAsync("https://jobs.example.com/analista-dados", "");

        Assert.Equal("url", result.Kind);
        Assert.Equal("Analista de Dados", result.Title);
        Assert.Equal("DataCo", result.Company);
        Assert.Equal("Sao Paulo, SP, BR", result.Location);
        Assert.StartsWith("Analista de Dados", result.Text);
        Assert.DoesNotContain("Titulo:", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SQL", result.Text);
        Assert.Contains("Power BI", result.Text);
        Assert.Contains("Descricao completa da vaga", result.Text);
    }

    private static string LinkedInHtml() => """
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

    private sealed class FakeHttpMessageHandler(Func<Uri?, string> htmlFactory, List<string>? requestedUrls = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requestedUrls?.Add(request.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(htmlFactory(request.RequestUri))
            });
        }
    }
}
