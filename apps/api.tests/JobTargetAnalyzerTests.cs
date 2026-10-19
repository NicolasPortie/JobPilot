using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public sealed class JobTargetAnalyzerTests
{
    private const string Posting = """
        Desenvolvedor Backend .NET Pleno
        Empresa: Fintech Horizonte Pagamentos
        Local: Remoto (Brasil) | Contratação CLT

        Sobre a vaga
        Estamos buscando uma pessoa Desenvolvedora Backend para construir APIs de alta disponibilidade.

        Responsabilidades
        - Desenvolver e manter APIs REST em C# e ASP.NET Core (.NET 8+)
        - Implementar mensageria com RabbitMQ
        - Containerizar serviços com Docker e publicar via GitHub Actions

        Requisitos
        - Experiência sólida com C#, .NET e Entity Framework Core
        - Conhecimento em SQL e otimização de queries
        - Vivência com Git e boas práticas (SOLID, Clean Code)
        - Inglês técnico para leitura

        Diferenciais
        - Experiência com Redis
        - Conhecimento em Kubernetes
        - Experiência no setor financeiro

        Benefícios
        VR/VA, plano de saúde, Gympass, PLR, cultura Lean.
        """;

    private static JobRequirementExtractionResponse Extract(string text) =>
        JobTargetAnalyzer.BuildDeterministicExtraction(new ResolvedJobSource("manual_text", text, "", "", "", "", [], []));

    private static string Importance(JobRequirementExtractionResponse response, string label) =>
        response.Requirements.Single(x => x.Label == label).Importance;

    [Fact]
    public void Extraction_UsesSectionToDecideImportance()
    {
        var result = Extract(Posting);

        Assert.Equal("must_have", Importance(result, "C#"));
        Assert.Equal("must_have", Importance(result, "Entity Framework Core"));
        Assert.Equal("must_have", Importance(result, "Docker"));
        Assert.Equal("must_have", Importance(result, "RabbitMQ"));
        Assert.Equal("nice_to_have", Importance(result, "Redis"));
        Assert.Equal("nice_to_have", Importance(result, "Kubernetes"));
    }

    [Fact]
    public void Extraction_IgnoresBenefitsAndAmbiguousTerms()
    {
        var labels = Extract(Posting).Requirements.Select(x => x.Label).ToArray();

        Assert.DoesNotContain("VR", labels);
        Assert.DoesNotContain("Lean", labels);
        Assert.DoesNotContain("C", labels);
        Assert.Contains("C#", labels);
    }

    [Fact]
    public void Extraction_DoesNotSplitAMoreSpecificTermIntoItsParts()
    {
        var labels = Extract(Posting).Requirements.Select(x => x.Label).ToArray();

        Assert.Contains("ASP.NET Core", labels);
        Assert.DoesNotContain("ASP.NET", labels);
        Assert.Contains(".NET 8", labels);
        Assert.Contains(".NET", labels);
        Assert.Contains("Entity Framework Core", labels);
        Assert.DoesNotContain("Entity Framework", labels);
    }

    [Fact]
    public void Extraction_ReadsCompanyLocationAndEnglishRequirement()
    {
        var result = Extract(Posting);

        Assert.Equal("Fintech Horizonte Pagamentos", result.Company);
        Assert.Equal("Remoto (Brasil)", result.Location);
        var english = result.Requirements.Single(x => x.Category == "language");
        Assert.Equal("Inglês", english.Label);
        Assert.Equal("must_have", english.Importance);
    }

    [Fact]
    public void Extraction_WithoutHeadingsFallsBackToContext()
    {
        var result = Extract("Precisamos de alguém com experiência em Python e Django para nosso time de dados, trabalho híbrido.");

        Assert.Contains(result.Requirements, x => x.Label == "Python");
        Assert.Contains(result.Requirements, x => x.Label == "Django");
        Assert.Equal("hybrid", result.Workplace);
    }

    [Fact]
    public void Extraction_UsesTheJobLineAsEvidence()
    {
        var docker = Extract(Posting).Requirements.Single(x => x.Label == "Docker");

        Assert.Equal("Containerizar serviços com Docker e publicar via GitHub Actions", docker.EvidenceText);
    }

    [Fact]
    public void Extraction_JavaDoesNotMatchJavaScript()
    {
        var labels = Extract("Requisitos:\n- JavaScript e TypeScript\n- React").Requirements.Select(x => x.Label).ToArray();

        Assert.Contains("JavaScript", labels);
        Assert.DoesNotContain("Java", labels);
    }
}
