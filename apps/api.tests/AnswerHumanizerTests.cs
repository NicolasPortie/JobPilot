using JobPilot.Api.Services;

namespace JobPilot.Api.Tests;

public class AnswerHumanizerTests
{
    [Fact]
    public void Humanize_RemovesCliches()
    {
        var input = "Sou um profissional dedicado com paixao por tecnologia. Estou em busca de novos desafios.";
        var result = AnswerHumanizer.Humanize(input);

        Assert.DoesNotContain("profissional dedicado", result.ToLowerInvariant());
        Assert.DoesNotContain("paixao por tecnologia", result.ToLowerInvariant());
        Assert.DoesNotContain("novos desafios", result.ToLowerInvariant());
    }

    [Fact]
    public void Humanize_RemovesBulletPoints()
    {
        var input = "- Experiencia com C#\n- Conhecimento em React\n- SQL Server";
        var result = AnswerHumanizer.Humanize(input);

        Assert.DoesNotContain("-", result);
        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void Humanize_ReplacesGenericAdjectives()
    {
        var input = "Tenho excelente experiencia com ASP.NET e incrivel conhecimento em bancos de dados.";
        var result = AnswerHumanizer.Humanize(input);

        Assert.DoesNotContain("excelente", result.ToLowerInvariant());
        Assert.DoesNotContain("incrivel", result.ToLowerInvariant());
    }

    [Fact]
    public void Humanize_AddsPeriodIfMissing()
    {
        var input = "Trabalho com C# e React";
        var result = AnswerHumanizer.Humanize(input);

        Assert.EndsWith(".", result);
    }

    [Fact]
    public void LooksLikeAi_DetectsAiText()
    {
        var input = "Sou um profissional dedicado com paixao por tecnologia. Estou em busca de novos desafios e gosto de trabalhar em equipe.";
        Assert.True(AnswerHumanizer.LooksLikeAi(input));
    }

    [Fact]
    public void LooksLikeAi_DoesNotDetectNaturalText()
    {
        var input = "Trabalho com C# ha uns 2 anos, mais por conta propria em projetos pessoais. Moro em SP e prefiro remoto.";
        Assert.False(AnswerHumanizer.LooksLikeAi(input));
    }

    [Fact]
    public void Humanize_KeepsRealDetails()
    {
        var input = "Tenho experiencia com C#, ASP.NET Core e React. Trabalhei em um projeto de e-commerce usando Entity Framework.";
        var result = AnswerHumanizer.Humanize(input);

        Assert.Contains("C#", result);
        Assert.Contains("ASP.NET Core", result);
        Assert.Contains("React", result);
        Assert.Contains("Entity Framework", result);
    }
}
