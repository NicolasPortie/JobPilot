using System.Globalization;
using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Valida valores preenchidos em campos de formulário.
/// Detecta erros óbvios (CPF com letras, email sem @, etc.)
/// para permitir self-healing antes de criar checkpoints.
/// </summary>
public sealed class FieldValidator
{
    private readonly FieldTypeClassifier _classifier = new();

    public ValidationResult Validate(ElementCandidate field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Fail("empty_value", "Valor preenchido está vazio.");

        var inferredType = _classifier.Infer(field);

        return inferredType switch
        {
            SemanticFieldType.Cpf => ValidateCpf(value),
            SemanticFieldType.Phone => ValidatePhone(value),
            SemanticFieldType.Email => ValidateEmail(value),
            SemanticFieldType.Salary => ValidateNumeric(value, field),
            SemanticFieldType.ExperienceYears => ValidateIntegerRange(value, 0, 40),
            SemanticFieldType.BirthDate => ValidateDate(value, field),
            SemanticFieldType.ZipCode => ValidateZipCode(value),
            _ => ValidationResult.Ok()
        };
    }

    private static ValidationResult ValidateCpf(string value)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length != 11)
            return ValidationResult.Fail("cpf_digit_count", $"CPF deve ter 11 dígitos, mas tem {digitsOnly.Length}. Valor: '{value}'");

        if (value.Any(c => char.IsLetter(c)))
            return ValidationResult.Fail("cpf_has_letters", $"CPF contém letras: '{value}'");

        if (IsCpfInvalid(digitsOnly))
            return ValidationResult.Fail("cpf_invalid", $"CPF com dígitos verificadores inválidos: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidatePhone(string value)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 10)
            return ValidationResult.Fail("phone_too_short", $"Telefone deve ter pelo menos 10 dígitos, mas tem {digitsOnly.Length}. Valor: '{value}'");

        if (digitsOnly.Length > 13)
            return ValidationResult.Fail("phone_too_long", $"Telefone tem {digitsOnly.Length} dígitos, parece incorreto. Valor: '{value}'");

        if (value.Any(c => char.IsLetter(c) && c != 'x' && c != 'X'))
            return ValidationResult.Fail("phone_has_letters", $"Telefone contém letras: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateEmail(string value)
    {
        if (!value.Contains('@'))
            return ValidationResult.Fail("email_no_at", $"Email não contém @: '{value}'");

        var parts = value.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return ValidationResult.Fail("email_invalid", $"Email malformado: '{value}'");

        if (!parts[1].Contains('.'))
            return ValidationResult.Fail("email_no_domain", $"Email sem domínio válido: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateNumeric(string value, ElementCandidate field)
    {
        var integerValue = value.Trim();
        var cleaned = value.Replace("R$", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("$", "")
                           .Replace(".", "")
                           .Replace(",", ".")
                           .Trim();

        if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return ValidationResult.Fail("not_numeric", $"Valor não é numérico: '{value}'");

        if (parsed <= 0)
            return ValidationResult.Fail("salary_not_positive", $"Pretensao salarial deve ser maior que zero: '{value}'");

        if (!Regex.IsMatch(integerValue, @"^\d+$") && parsed < 100)
            return ValidationResult.Fail("salary_too_low", $"Pretensao salarial formatada parece baixa demais: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateIntegerRange(string value, int min, int max)
    {
        var cleaned = value.Trim();
        if (!Regex.IsMatch(cleaned, @"^\d{1,2}$"))
            return ValidationResult.Fail("not_integer", $"Valor deve ser um numero inteiro curto: '{value}'");

        var number = int.Parse(cleaned, CultureInfo.InvariantCulture);
        if (number < min || number > max)
            return ValidationResult.Fail("integer_out_of_range", $"Valor numerico fora da faixa esperada: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateDate(string value, ElementCandidate field)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 8)
        {
            // Formato DDMMYYYY ou similar.
            if (int.TryParse(digitsOnly[..2], out var day) && int.TryParse(digitsOnly[2..4], out var month))
            {
                if (day > 31 || month > 12)
                    return ValidationResult.Fail("date_invalid", $"Data com dia ou mês inválido: '{value}'");
            }
        }

        if (digitsOnly.Length < 6)
            return ValidationResult.Fail("date_too_short", $"Data parece incompleta: '{value}'");

        return ValidationResult.Ok();
    }

    private static ValidationResult ValidateZipCode(string value)
    {
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length != 8)
            return ValidationResult.Fail("cep_length", $"CEP deve ter 8 dígitos, mas tem {digitsOnly.Length}. Valor: '{value}'");
        return ValidationResult.Ok();
    }

    private static bool IsCpfInvalid(string digitsOnly)
    {
        if (digitsOnly.Length != 11) return true;
        if (digitsOnly.Distinct().Count() == 1) return true;

        var digits = digitsOnly.Select(c => c - '0').ToArray();

        var sum1 = 0;
        for (var i = 0; i < 9; i++) sum1 += digits[i] * (10 - i);
        var digit1 = (sum1 * 10) % 11;
        if (digit1 == 10) digit1 = 0;
        if (digit1 != digits[9]) return true;

        var sum2 = 0;
        for (var i = 0; i < 10; i++) sum2 += digits[i] * (11 - i);
        var digit2 = (sum2 * 10) % 11;
        if (digit2 == 10) digit2 = 0;
        if (digit2 != digits[10]) return true;

        return false;
    }
}

public sealed record ValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage)
{
    public static ValidationResult Ok() => new(true, null, null);
    public static ValidationResult Fail(string code, string message) => new(false, code, message);
}
