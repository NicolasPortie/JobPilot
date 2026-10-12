namespace JobPilot.Agent.Application;

/// <summary>
/// Classifica erros como retriáveis (tentar de novo) ou permanentes (criar checkpoint).
/// </summary>
public sealed class ErrorClassifier
{
    private const int MaxRetries = 3;

    public ErrorCategory Classify(string actionType, string? errorCode, string? errorMessage, int retryCount)
    {
        if (retryCount >= MaxRetries)
            return ErrorCategory.HumanRequired;

        var code = (errorCode ?? "").ToLowerInvariant();
        var msg = (errorMessage ?? "").ToLowerInvariant();

        // SEMPRE retriável: transientes e de validação
        if (code.Contains("timeout")) return ErrorCategory.Retriable;
        if (code.Contains("element_not_observed")) return ErrorCategory.Retriable;
        if (code.Contains("validation_failed")) return ErrorCategory.Retriable;
        if (code.Contains("stuck")) return ErrorCategory.Retriable;
        if (code.Contains("empty_value")) return ErrorCategory.Retriable;
        if (code.Contains("cpf_")) return ErrorCategory.Retriable;
        if (code.Contains("phone_")) return ErrorCategory.Retriable;
        if (code.Contains("email_")) return ErrorCategory.Retriable;
        if (code.Contains("date_")) return ErrorCategory.Retriable;
        if (code.Contains("not_numeric")) return ErrorCategory.Retriable;

        // SEMPRE humano: segurança e login
        if (code.Contains("captcha")) return ErrorCategory.HumanRequired;
        if (code.Contains("login")) return ErrorCategory.HumanRequired;
        if (code.Contains("security")) return ErrorCategory.HumanRequired;
        if (code.Contains("payment")) return ErrorCategory.HumanRequired;
        if (code.Contains("2fa")) return ErrorCategory.HumanRequired;
        if (msg.Contains("captcha")) return ErrorCategory.HumanRequired;
        if (msg.Contains("verify your identity")) return ErrorCategory.HumanRequired;

        // Default: tentar de novo (conservador)
        return ErrorCategory.Retriable;
    }
}

public enum ErrorCategory
{
    /// <summary>Pode tentar novamente com estratégia diferente.</summary>
    Retriable,
    /// <summary>Erro permanente, mas não precisa de humano (ex: vaga já foi removida).</summary>
    Permanent,
    /// <summary>Precisa de intervenção humana.</summary>
    HumanRequired
}
