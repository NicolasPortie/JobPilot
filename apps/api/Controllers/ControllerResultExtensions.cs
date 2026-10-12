using JobPilot.Api.Application;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

internal static class ControllerResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result, string? createdLocation = null) =>
        result.Status switch
        {
            ServiceResultStatus.Ok => controller.Ok(result.Value),
            ServiceResultStatus.Created => controller.Created(createdLocation ?? "", result.Value),
            ServiceResultStatus.Accepted => controller.Accepted(createdLocation ?? "", result.Value),
            ServiceResultStatus.NotFound => controller.NotFound(),
            ServiceResultStatus.NoContent => controller.NoContent(),
            ServiceResultStatus.Invalid => controller.BadRequest(CreateProblemDetails(controller, StatusCodes.Status400BadRequest, result.Error)),
            ServiceResultStatus.Conflict => controller.Conflict(CreateProblemDetails(controller, StatusCodes.Status409Conflict, result.Error)),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, CreateProblemDetails(controller, StatusCodes.Status500InternalServerError, "Erro interno do servidor"))
        };

    private static ProblemDetails CreateProblemDetails(ControllerBase controller, int status, string? detail)
    {
        var request = controller.HttpContext.Request;
        return new ProblemDetails
        {
            Type = $"https://jobpilot.io/errors/{status}",
            Title = status switch
            {
                StatusCodes.Status400BadRequest => "Requisicao invalida",
                StatusCodes.Status409Conflict => "Conflito",
                StatusCodes.Status500InternalServerError => "Erro interno do servidor",
                _ => "Erro"
            },
            Status = status,
            Detail = detail,
            Instance = $"{request.Method} {request.Path}"
        };
    }
}
