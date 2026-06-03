using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class Camada1_FieldTypeClassifierTests
{
    private readonly FieldTypeClassifier _classifier = new();

    [Fact]
    public void Infer_CpfPorPlaceholder_MascaraCompleta()
    {
        var field = new ElementCandidate(
            "id1", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.Cpf, type);
    }

    [Fact]
    public void Infer_CpfPorPattern_RegexOnzeDigitos()
    {
        var field = new ElementCandidate(
            "id2", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Pattern: @"\d{3}\.\d{3}\.\d{3}-\d{2}");

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.Cpf, type);
    }

    [Fact]
    public void Infer_TelefonePorPlaceholder_MascaraBrasil()
    {
        var field = new ElementCandidate(
            "id3", "input", "text", "", "Telefone", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "(99) 99999-9999");

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.Phone, type);
    }

    [Fact]
    public void Infer_EmailPorAutocomplete_Email()
    {
        var field = new ElementCandidate(
            "id4", "input", "email", "", "Email", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            AutoComplete: "email");

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.Email, type);
    }

    [Fact]
    public void Infer_NomeCompletoPorLabel_Keywords()
    {
        var field = new ElementCandidate(
            "id5", "input", "text", "", "Nome completo", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.FullName, type);
    }

    [Fact]
    public void Infer_DataNascimentoPorLabel_Keywords()
    {
        var field = new ElementCandidate(
            "id6", "input", "text", "", "Data de nascimento", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.BirthDate, type);
    }

    [Fact]
    public void Infer_LinkedinPorName_Attribute()
    {
        var field = new ElementCandidate(
            "id7", "input", "text", "", "LinkedIn", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Name: "linkedin");

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.LinkedInUrl, type);
    }

    [Fact]
    public void Infer_CepPorLabel_Keywords()
    {
        var field = new ElementCandidate(
            "id8", "input", "text", "", "CEP", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.ZipCode, type);
    }

    [Fact]
    public void Confidence_CpfComMascaraPlaceholder_AltaConfianca()
    {
        var field = new ElementCandidate(
            "id9", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var type = _classifier.Infer(field);
        var confidence = _classifier.Confidence(field, type);

        Assert.True(confidence >= 0.8m, $"Expected high confidence, got {confidence}");
    }

    [Fact]
    public void Infer_CampoSemSinais_Unknown()
    {
        var field = new ElementCandidate(
            "id10", "input", "text", "", "", "", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var type = _classifier.Infer(field);

        Assert.Equal(SemanticFieldType.Unknown, type);
    }
}
