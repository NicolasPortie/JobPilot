using JobPilot.Agent.Application;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Tests;

public class Camada3_FieldValidatorTests
{
    private readonly FieldTypeClassifier _classifier = new();
    private readonly FieldValidator _validator = new();

    [Fact]
    public void Validate_CpfValido_Passa()
    {
        var field = new ElementCandidate(
            "id1", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var result = _validator.Validate(field, "123.456.789-09");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CpfInvalidoDigitos_Falha()
    {
        var field = new ElementCandidate(
            "id2", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var result = _validator.Validate(field, "123.456.789-00");
        Assert.False(result.IsValid);
        Assert.Contains("dígitos verificadores", result.ErrorMessage);
    }

    [Fact]
    public void Validate_CpfComLetras_Falha()
    {
        var field = new ElementCandidate(
            "id3", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var result = _validator.Validate(field, "abc.def.ghi-jk");
        Assert.False(result.IsValid);
        Assert.Contains("11 dígitos", result.ErrorMessage);
    }

    [Fact]
    public void Validate_CpfTamanhoErrado_Falha()
    {
        var field = new ElementCandidate(
            "id4", "input", "text", "", "CPF", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "000.000.000-00");

        var result = _validator.Validate(field, "123.456.789-0");
        Assert.False(result.IsValid);
        Assert.Contains("11 dígitos", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmailValido_Passa()
    {
        var field = new ElementCandidate(
            "id5", "input", "email", "", "Email", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "nicolas@email.com");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmailSemArroba_Falha()
    {
        var field = new ElementCandidate(
            "id6", "input", "email", "", "Email", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "nicolas.email.com");
        Assert.False(result.IsValid);
        Assert.Contains("@", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmailSemDominio_Falha()
    {
        var field = new ElementCandidate(
            "id7", "input", "email", "", "Email", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "nicolas@");
        Assert.False(result.IsValid);
        Assert.True(result.ErrorCode == "email_invalid" || result.ErrorCode == "email_no_domain",
            $"Expected email_invalid or email_no_domain, got {result.ErrorCode}");
    }

    [Fact]
    public void Validate_TelefoneValido_Passa()
    {
        var field = new ElementCandidate(
            "id8", "input", "text", "", "Telefone", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "(99) 99999-9999");

        var result = _validator.Validate(field, "(11) 99999-8888");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TelefoneCurto_Falha()
    {
        var field = new ElementCandidate(
            "id9", "input", "text", "", "Telefone", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0),
            Placeholder: "(99) 99999-9999");

        var result = _validator.Validate(field, "119999");
        Assert.False(result.IsValid);
        Assert.Contains("pelo menos 10", result.ErrorMessage);
    }

    [Fact]
    public void Validate_DataValida_Passa()
    {
        var field = new ElementCandidate(
            "id10", "input", "text", "", "Data de nascimento", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "01/01/1990");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DataInvalida_Falha()
    {
        var field = new ElementCandidate(
            "id11", "input", "text", "", "Data de nascimento", "", "", true, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "32/13/1990");
        Assert.False(result.IsValid);
        Assert.Contains("dia ou mês inválido", result.ErrorMessage);
    }

    [Fact]
    public void Validate_CampoUnknown_SemprePassa()
    {
        var field = new ElementCandidate(
            "id12", "input", "text", "", "", "", "", false, false, false, "",
            [], new ElementBounds(0, 0, 0, 0));

        var result = _validator.Validate(field, "qualquer coisa");
        Assert.True(result.IsValid);
    }
}

public class Camada3_ErrorClassifierTests
{
    private readonly ErrorClassifier _classifier = new();

    [Fact]
    public void Classify_Timeout_Retriable()
    {
        var cat = _classifier.Classify("fill", "timeout", "Element not found", 0);
        Assert.Equal(ErrorCategory.Retriable, cat);
    }

    [Fact]
    public void Classify_ValidationFailed_Retriable()
    {
        var cat = _classifier.Classify("fill", "validation_failed", "CPF inválido", 0);
        Assert.Equal(ErrorCategory.Retriable, cat);
    }

    [Fact]
    public void Classify_Captcha_HumanRequired()
    {
        var cat = _classifier.Classify("fill", null, "captcha detected", 0);
        Assert.Equal(ErrorCategory.HumanRequired, cat);
    }

    [Fact]
    public void Classify_Login_HumanRequired()
    {
        var cat = _classifier.Classify("fill", "login_required", "login required", 0);
        Assert.Equal(ErrorCategory.HumanRequired, cat);
    }

    [Fact]
    public void Classify_MaxRetries_HumanRequired()
    {
        var cat = _classifier.Classify("fill", "timeout", "Element not found", 3);
        Assert.Equal(ErrorCategory.HumanRequired, cat);
    }

    [Fact]
    public void Classify_ElementNotObserved_Retriable()
    {
        var cat = _classifier.Classify("fill", "element_not_observed", "Gone", 0);
        Assert.Equal(ErrorCategory.Retriable, cat);
    }

    [Fact]
    public void Classify_Unknown_Retriable()
    {
        var cat = _classifier.Classify("fill", "unexpected", "Something went wrong", 0);
        Assert.Equal(ErrorCategory.Retriable, cat);
    }
}
