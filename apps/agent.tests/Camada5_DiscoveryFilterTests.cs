using JobPilot.Agent.Discovery;
using JobPilot.Agent.Domain;
using Xunit.Abstractions;

namespace JobPilot.Agent.Tests;

public class Camada5_DiscoveryFilterTests
{
    [Fact]
    public void PassesFilters_DevDotNet_TituloCorreto_Passa()
    {
        var job = new DiscoveredJobDto("Desenvolvedor .NET Sênior", "TechCorp", "São Paulo", "", "https://linkedin.com/jobs/view/1", false);
        var filter = new JobFilter([], [], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "asp.net desenvolvedor");

        Assert.True(result);
    }

    [Fact]
    public void PassesFilters_ExecutivoVendas_RemotoNoLocal_Rejeita()
    {
        // BUG ANTIGO: a keyword "remoto" estava no local, então a vaga passava
        var job = new DiscoveredJobDto("Executivo de Vendas", "Empresa X", "São Paulo, Remoto", "", "https://linkedin.com/jobs/view/2", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "asp.net desenvolvedor remoto");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_KeywordTechNaEmpresa_NaoNoTitulo_Rejeita()
    {
        // A empresa pode ter "Microsoft" ou "Tech" no nome, mas o título é de outra área
        var job = new DiscoveredJobDto("Auxiliar Administrativo", "TechCorp Software", "São Paulo", "", "https://linkedin.com/jobs/view/3", false);
        var filter = new JobFilter([], [], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "software developer");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_BuscaTech_VagaDeVendas_Rejeita()
    {
        var job = new DiscoveredJobDto("Representante de Vendas", "Empresa", "Remoto", "", "https://linkedin.com/jobs/view/4", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "developer remoto");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_SoGenericaRemoto_VagaDeVendas_Rejeita()
    {
        // Só tem palavras genéricas (remoto) - exige match no título
        var job = new DiscoveredJobDto("Executivo de Vendas", "Empresa", "Remoto", "", "https://linkedin.com/jobs/view/5", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "remoto");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_SoGenericaJunior_DevJunior_Passa()
    {
        // Só tem palavras genéricas mas o título tem a palavra genérica
        var job = new DiscoveredJobDto("Desenvolvedor Java Junior", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/6", false);
        var filter = new JobFilter([], [], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "junior");

        Assert.True(result);
    }

    [Fact]
    public void PassesFilters_BlockedSeniority_Rejeita()
    {
        var job = new DiscoveredJobDto("Desenvolvedor .NET Sênior", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/7", false);
        var filter = new JobFilter([], ["senior"], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "asp.net desenvolvedor");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_NaoRemoto_QuandoExigeRemote_Rejeita()
    {
        var job = new DiscoveredJobDto("Desenvolvedor .NET", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/8", false);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "asp.net desenvolvedor");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_AnalistaDados_TituloCorreto_Passa()
    {
        var job = new DiscoveredJobDto("Analista de Dados", "Empresa", "Remoto", "", "https://linkedin.com/jobs/view/9", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        // Keywords em português batem com o título em português
        var result = JobDiscoverer.PassesFilters(job, filter, "analista dados remoto");

        Assert.True(result);
    }

    [Fact]
    public void PassesFilters_Motorista_BuscaDev_Rejeita()
    {
        var job = new DiscoveredJobDto("Motorista de Entrega", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/10", false);
        var filter = new JobFilter([], [], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "developer");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_QA_Tester_BuscaQA_Passa()
    {
        var job = new DiscoveredJobDto("QA Tester", "Empresa", "Remoto", "", "https://linkedin.com/jobs/view/11", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "qa tester remoto");

        Assert.True(result);
    }

    private readonly ITestOutputHelper _output;
    public Camada5_DiscoveryFilterTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PassesFilters_BuscaAspNet_VagaJava_Rejeita()
    {
        // BUG: busca "asp.net desenvolvedor" não pode trazer vaga de Java
        var job = new DiscoveredJobDto("Desenvolvedor(a) Java Pleno", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/1", false);
        var filter = new JobFilter([], [], [], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "asp.net desenvolvedor");
        _output.WriteLine($"PassesFilters result: {result}");

        Assert.False(result);
    }

    [Fact]
    public void PassesFilters_SemKeywords_SemFiltro_Passa()
    {
        var job = new DiscoveredJobDto("Qualquer Coisa", "Empresa", "São Paulo", "", "https://linkedin.com/jobs/view/12", false);
        JobFilter? filter = null;

        var result = JobDiscoverer.PassesFilters(job, filter, "");

        Assert.True(result);
    }

    [Fact]
    public void PassesFilters_VagasReais_LinkedIn_AspNet_Passam()
    {
        var filter = new JobFilter([], [], [], [], [], 0, false);
        var keywords = "asp.net desenvolvedor";

        // Vagas reais que apareceram no LinkedIn para busca de ASP.NET
        Assert.True(JobDiscoverer.PassesFilters(new DiscoveredJobDto("Desenvolvedor .NET Full Stack", "Mirante", "Brasil", "", "", false), filter, keywords));
        Assert.True(JobDiscoverer.PassesFilters(new DiscoveredJobDto("Pessoa Desenvolvedora | C# - Asp Net - Jr / Pl", "Dimensa", "Brasil", "", "", false), filter, keywords));
        Assert.True(JobDiscoverer.PassesFilters(new DiscoveredJobDto(".NET Developer", "Perform", "São Paulo", "", "", false), filter, keywords));
        Assert.True(JobDiscoverer.PassesFilters(new DiscoveredJobDto("Analista Desenvolvedor .Net", "Jobgether", "São Paulo", "", "", false), filter, keywords));
    }

    [Fact]
    public void PassesFilters_JavaScript_NoTitulo_Passa()
    {
        var job = new DiscoveredJobDto("Engenheiro de Software JavaScript", "Empresa", "Remoto", "", "https://linkedin.com/jobs/view/13", true);
        var filter = new JobFilter([], [], ["remote"], [], [], 0, false);

        var result = JobDiscoverer.PassesFilters(job, filter, "javascript engineer remoto");

        Assert.True(result);
    }
}
