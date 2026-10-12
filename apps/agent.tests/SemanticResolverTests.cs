using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class SemanticResolverTests
{
    private readonly FieldTypeClassifier _classifier = new();
    private readonly SemanticProfileMapper _mapper = new();

    private static CandidateProfile CreateProfile() => new()
    {
        FullName = "Nicolas Portie Suss",
        Cpf = "12345678909",
        Email = "nicolas@email.com",
        Phone = "11999998888",
        City = "São Paulo",
        LinkedinUrl = "https://linkedin.com/in/nicolas",
        SalaryExpectation = "5000"
    };

    [Fact]
    public void Map_CpfField_RetornaCpfFormatado()
    {
        var field = new ElementCandidate(
            "id1", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("123.456.789-09", value);
    }

    [Fact]
    public void Map_TelefoneField_RetornaTelefoneFormatado()
    {
        var field = new ElementCandidate(
            "id2", "input", "text", "", "Telefone", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "(99) 99999-9999");

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("(11) 99999-8888", value);
    }

    [Fact]
    public void Map_EmailField_RetornaEmail()
    {
        var field = new ElementCandidate(
            "id3", "input", "email", "", "Email", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("nicolas@email.com", value);
    }

    [Fact]
    public void Map_NomeCompletoField_RetornaNome()
    {
        var field = new ElementCandidate(
            "id4", "input", "text", "", "Nome completo", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("Nicolas Portie Suss", value);
    }

    [Fact]
    public void Map_LastNameField_RetornaApenasUltimoSobrenome()
    {
        var field = new ElementCandidate(
            "id4-last", "input", "text", "", "Last name", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            AutoComplete: "family-name");
        var profile = CreateProfile();
        profile.FullName = "Nicolas Portie Sussai Silva";

        var ctx = new SessionContext();
        var value = _mapper.Map(field, profile, ctx, []);

        Assert.Equal("Silva", value);
    }

    [Fact]
    public void Map_CidadeField_RetornaCidade()
    {
        var field = new ElementCandidate(
            "id5", "input", "text", "", "Cidade", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("São Paulo", value);
    }

    [Fact]
    public void Select_Cidade_EhCampoDeSelecao_NaoTexto()
    {
        var field = new ElementCandidate(
            "id5s", "select", "", "", "Cidade", "", "", true, false, false, "",
            ["Selecione", "Penápolis", "São Paulo"], new ElementBounds(0, 0, 0, 0));

        Assert.True(field.IsSelect);
        Assert.False(field.IsTextEntry);
    }

    [Fact]
    public void Map_LinkedinField_RetornaUrl()
    {
        var field = new ElementCandidate(
            "id6", "input", "text", "", "LinkedIn", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("https://linkedin.com/in/nicolas", value);
    }

    [Fact]
    public void Map_SalarioField_RetornaSalarioNormalizado()
    {
        var field = new ElementCandidate(
            "id7", "input", "text", "", "Expected salary", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            InputMode: "numeric");

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Equal("5000", value);
    }

    [Fact]
    public void Map_PerfilVazio_RetornaNull()
    {
        var field = new ElementCandidate(
            "id8", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var ctx = new SessionContext();
        var emptyProfile = new CandidateProfile();
        var value = _mapper.Map(field, emptyProfile, ctx, []);

        Assert.Null(value);
    }

    [Fact]
    public void Map_CampoUnknown_RetornaNull()
    {
        var field = new ElementCandidate(
            "id9", "input", "text", "", "", "", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var ctx = new SessionContext();
        var value = _mapper.Map(field, CreateProfile(), ctx, []);

        Assert.Null(value);
    }

    [Fact]
    public void FormatCpf_Valido_FormataCorretamente()
    {
        var formatted = SemanticProfileMapper.FormatCpf("12345678909");
        Assert.Equal("123.456.789-09", formatted);
    }

    [Fact]
    public void FormatCpf_Nulo_RetornaVazio()
    {
        var formatted = SemanticProfileMapper.FormatCpf(null);
        Assert.Equal("", formatted);
    }

    [Fact]
    public void FormatPhone_ComPlaceholder_Formata()
    {
        var formatted = SemanticProfileMapper.FormatPhone("11999998888", "(99) 99999-9999", "");
        Assert.Equal("(11) 99999-8888", formatted);
    }

    [Fact]
    public void NormalizeSalary_ValorNumerico_LimpaFormatacao()
    {
        var field = new ElementCandidate(
            "id10", "input", "text", "", "", "", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            InputMode: "numeric");

        var normalized = SemanticProfileMapper.NormalizeSalary("R$ 5.000,00", field);
        Assert.Equal("5000", normalized);
    }

    [Fact]
    public void NormalizeSalary_DecimalBrasileiro_RetornaInteiro()
    {
        var field = new ElementCandidate(
            "id11", "input", "text", "", "Qual e a sua pretensao salarial?", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var normalized = SemanticProfileMapper.NormalizeSalary("3800,00", field);

        Assert.Equal("3800", normalized);
    }

    [Fact]
    public void NormalizeSalary_MascaraMoedaComCentavos_RetornaCentavos()
    {
        var field = new ElementCandidate(
            "id12", "input", "text", "", "Pretensao salarial como CLT", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "R$ 0.000,00");

        var normalized = SemanticProfileMapper.NormalizeSalary("3800,00", field);

        Assert.Equal("3800000", normalized);
    }
}


