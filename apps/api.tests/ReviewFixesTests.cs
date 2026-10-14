using JobPilot.Api.Services;
using JobPilot.Api.Validators;

namespace JobPilot.Api.Tests;

public sealed class ReviewFixesTests
{
    [Theory]
    [InlineData("Atuei em ERP entre 2019-2023 e depois 2023 2025.", false)]
    [InlineData("Equipe de 10000 5000 usuários.", false)]
    [InlineData("Contato: (18) 99664-6794 no topo", true)]
    [InlineData("Escreva para ana@example.com", true)]
    [InlineData("PERFIL PROFISSIONAL Desenvolvedor", true)]
    public void IsPollutedSummary_OnlyFlagsContactDataAndHeadings(string summary, bool polluted) =>
        Assert.Equal(polluted, ResumeProfileInference.IsPollutedSummary(summary));

    [Fact]
    public void CleanSummaryText_OnlyCutsAtUppercaseSectionLabels()
    {
        var kept = ResumeTextSanitizer.CleanSummaryText("Atuei em projetos de ERP e na formação de times.", null, null);
        var cut = ResumeTextSanitizer.CleanSummaryText("Atuei em ERP. COMPETENCIAS C#, .NET", null, null);

        Assert.Equal("Atuei em projetos de ERP e na formação de times", kept);
        Assert.Equal("Atuei em ERP", cut);
    }

    [Theory]
    [InlineData("https://empresa.com/vaga", true)]
    [InlineData("http://empresa.com/vaga", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,x", false)]
    [InlineData("empresa.com/vaga", false)]
    public void QuickTailorRequest_JobUrlMustBeHttp(string url, bool valid) =>
        Assert.Equal(valid, new QuickTailorRequestValidator().Validate(new QuickTailorRequest("Texto da vaga", url)).IsValid);

    [Fact]
    public void OrganizeJobTarget_ManualTextWithNonHttpUrlIsRejected() =>
        Assert.False(new OrganizeJobTargetRequestValidator().Validate(new OrganizeJobTargetRequest("manual_text", "Texto", "javascript:alert(1)")).IsValid);

    [Fact]
    public void Parse_ShortDescriptionAfterHeaderStaysADescription()
    {
        var parsed = ResumeStructureParser.Parse("Ana\nEXPERIÊNCIA\nAcme (2020 - 2022)\nSuporte a clientes e rotinas.");

        var job = Assert.Single(parsed.Experience);
        Assert.Equal("Acme", job["company"]);
        Assert.False(job.ContainsKey("role"));
        Assert.Equal("Suporte a clientes e rotinas.", job["description"]);
    }
}
