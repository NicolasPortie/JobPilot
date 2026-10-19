using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public sealed class ResumeStructureParserTests
{
    private const string SampleResume = """
        NICOLAS PORTIE
        Engenheiro de Software | Backend, APIs e Aplicações Corporativas
        Penápolis/SP | (18) 99664-6794 | contato@nicolasportie.com | LinkedIn | GitHub | Portfólio
        PERFIL PROFISSIONAL
        Engenheiro de Software com foco no desenvolvimento e evolução de aplicações corporativas, com forte atuação em
        backend, estruturação de APIs e integrações.
        SKILLS E TECNOLOGIAS
        ● Stack Principal & DB: C#, .NET (8 a 10), ASP.NET Core, EF Core, Blazor, Next.js, Delphi | SQL Server, PostgreSQL,
        Firebird, Redis.
        ● Arquitetura & Ferramentas: Integração de APIs, Multi-tenant, Docker, Clean Code, SOLID, Testes Unitários,
        Git/GitHub.
        EXPERIÊNCIA PROFISSIONAL
        Irrigação Penápolis | Programador de Sistemas (Abr/2025 - Atual)
        ● Evolução e Arquitetura: Liderando a evolução da plataforma para ASP.NET (.NET 9) e estruturando arquitetura
        multi-tenant com EF Core, garantindo isolamento seguro de dados.
        ● Performance Operacional: Otimização contínua de banco de dados e refatoração de consultas críticas.
        Eficaz Sistemas | Analista de Suporte / Desenvolvedor (Mai/2024 - Dez/2024)
        ● Sustentação de ERPs: Manutenção evolutiva de regras de negócio corporativas (Delphi).
        PROJETOS DE DESTAQUE
        ● ControlFinance (Gestão e IA): Plataforma financeira (.NET 10, PostgreSQL, Next.js, Docker). Integração de IA para
        leitura automática de recibos.
        ● CaotinhoAuMiau (Gestão de Adoção): Sistema de adoção pet (.NET 9, SQL Server, Redis).
        FORMAÇÃO E CERTIFICAÇÕES
        ● Acadêmico: Bacharelado em Engenharia da Computação – UNISALESIANO Araçatuba (2025).
        ● Idiomas: Inglês (Técnico para documentações/código) | Português (Nativo).
        ● Certificações: Gestão de Projetos Ágeis (Santander, 2026) | Metodologias Ágeis, Clean Code e TDD (2025).
        PDF hyperlinks:
        https://www.linkedin.com/in/nicolasportie/
        """;

    [Fact]
    public void Parse_SeparatesHeaderSummaryAndSkills()
    {
        var parsed = ResumeStructureParser.Parse(SampleResume);

        Assert.Equal("Nicolas Portie", parsed.FullName);
        Assert.Equal("Engenheiro de Software | Backend, APIs e Aplicações Corporativas", parsed.Headline);
        Assert.StartsWith("Engenheiro de Software com foco", parsed.Summary);
        Assert.DoesNotContain("@", parsed.Summary);
        Assert.DoesNotContain("PERFIL PROFISSIONAL", parsed.Summary);
        Assert.Contains("C#", parsed.Skills);
        Assert.Contains(".NET", parsed.Skills);
        Assert.Contains("Git", parsed.Skills);
        Assert.Contains("GitHub", parsed.Skills);
        Assert.Contains("Redis", parsed.Skills);
        Assert.DoesNotContain(parsed.Skills, skill => skill.Contains("Stack Principal"));
    }

    [Fact]
    public void Parse_SplitsExperienceIntoCompanyRolePeriodAndBullets()
    {
        var parsed = ResumeStructureParser.Parse(SampleResume);

        Assert.Equal(2, parsed.Experience.Count);
        var first = parsed.Experience[0];
        Assert.Equal("Irrigação Penápolis", first["company"]);
        Assert.Equal("Programador de Sistemas", first["role"]);
        Assert.Equal("Abr/2025 - Atual", first["period"]);
        var bullets = first["description"].Split('\n');
        Assert.Equal(2, bullets.Length);
        Assert.Contains("multi-tenant com EF Core", bullets[0]);
        Assert.StartsWith("Performance Operacional", bullets[1]);

        var second = parsed.Experience[1];
        Assert.Equal("Eficaz Sistemas", second["company"]);
        Assert.Equal("Analista de Suporte / Desenvolvedor", second["role"]);
        Assert.Equal("Mai/2024 - Dez/2024", second["period"]);
    }

    [Fact]
    public void Parse_ReadsProjectsEducationCertificationsAndLanguages()
    {
        var parsed = ResumeStructureParser.Parse(SampleResume);

        Assert.Equal(2, parsed.Projects.Count);
        Assert.Equal("ControlFinance (Gestão e IA)", parsed.Projects[0]["name"]);
        Assert.StartsWith("Plataforma financeira", parsed.Projects[0]["description"]);

        var education = Assert.Single(parsed.Education);
        Assert.Equal("Bacharelado em Engenharia da Computação", education["degree"]);
        Assert.Equal("UNISALESIANO Araçatuba", education["institution"]);
        Assert.Equal("2025", education["year"]);

        Assert.Equal(2, parsed.Languages.Count);
        Assert.Equal("Inglês", parsed.Languages[0]["language"]);
        Assert.Equal("Técnico para documentações/código", parsed.Languages[0]["level"]);

        Assert.Equal(2, parsed.Certifications.Count);
        Assert.Equal("Gestão de Projetos Ágeis", parsed.Certifications[0]["name"]);
        Assert.Equal("Santander", parsed.Certifications[0]["issuer"]);
        Assert.Equal("2026", parsed.Certifications[0]["year"]);
    }

    [Fact]
    public void Parse_WorksWhenTheWholeResumeIsOneFlattenedLine()
    {
        var flattened = string.Join(" ", SampleResume.Split('\n')
            .Where(line => !line.StartsWith("PDF") && !line.StartsWith("https"))
            .Select(line => line.Trim()));

        var parsed = ResumeStructureParser.Parse(flattened);

        Assert.Equal(2, parsed.Experience.Count);
        Assert.Equal("Programador de Sistemas", parsed.Experience[0]["role"]);
        Assert.Equal(2, parsed.Projects.Count);
        Assert.NotEmpty(parsed.Skills);
        Assert.StartsWith("Engenheiro de Software com foco", parsed.Summary);
    }

    [Fact]
    public void Parse_HandlesRoleFirstAndSeparatePeriodLine()
    {
        var parsed = ResumeStructureParser.Parse("""
            Maria Souza
            EXPERIÊNCIA
            Desenvolvedora Backend - Acme Ltda
            2021 - 2023
            • Criou APIs REST em Java.
            Estagiária | Beta SA | Jan/2019 - Dez/2020
            • Suporte a clientes.
            """);

        Assert.Equal(2, parsed.Experience.Count);
        Assert.Equal("Desenvolvedora Backend", parsed.Experience[0]["role"]);
        Assert.Equal("Acme Ltda", parsed.Experience[0]["company"]);
        Assert.Equal("2021 - 2023", parsed.Experience[0]["period"]);
        Assert.Equal("Criou APIs REST em Java.", parsed.Experience[0]["description"]);
        Assert.Equal("Jan/2019 - Dez/2020", parsed.Experience[1]["period"]);
    }

    [Fact]
    public void Parse_TextWithoutHeadingsHasNoStructure()
    {
        var parsed = ResumeStructureParser.Parse("Fulano de Tal\nTrabalho com vendas há anos.");

        Assert.False(parsed.HasStructure);
        Assert.Empty(parsed.Skills);
    }

    [Fact]
    public void BuildPlainResume_UsesCleanSectionsWithoutRawTextDuplicate()
    {
        var data = new ResumeProfileImportData("Nicolas Portie Sussai Silva", "n@x.com", "(18) 99664-6794", "Penápolis", "SP", "Brasil", "", "", "", "Resumo limpo.", ["C#", "SQL"], "", "unknown");

        var text = ResumeImportSectionBuilder.BuildPlainResume(data, SampleResume);

        Assert.StartsWith("Nicolas Portie Sussai Silva", text);
        Assert.Contains("EXPERIÊNCIA PROFISSIONAL", text);
        Assert.Contains("Programador de Sistemas - Irrigação Penápolis | Abr/2025 - Atual", text);
        Assert.Contains("- Performance Operacional", text);
        Assert.DoesNotContain("TEXTO IMPORTADO", text);
        Assert.DoesNotContain("PDF hyperlinks", text);
        Assert.Single(Regex.Matches(text, "Performance Operacional"));
    }
}
