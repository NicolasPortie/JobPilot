using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JobPilot.Api.Infrastructure;

internal sealed class ValidationFilter(IServiceProvider serviceProvider) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var argument in context.ActionArguments.Values.Where(v => v is not null).Cast<object>())
        {
            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (serviceProvider.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = validator.Validate(validationContext);

            if (result.IsValid)
                continue;

            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Type = "https://jobpilot.io/errors/400",
                Title = "Validacao falhou",
                Status = StatusCodes.Status400BadRequest,
                Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
            });
            return;
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
