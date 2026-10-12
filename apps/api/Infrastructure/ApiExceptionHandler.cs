using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Infrastructure;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = CreateProblemDetails(httpContext, exception);
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Requisicao invalida"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Requisicao invalida"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso nao encontrado"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
        };

        return new ProblemDetails
        {
            Type = $"https://jobpilot.io/errors/{status}",
            Title = title,
            Status = status,
            Detail = exception.Message,
            Instance = $"{context.Request.Method} {context.Request.Path}"
        };
    }
}
