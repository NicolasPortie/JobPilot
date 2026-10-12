using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace JobPilot.Api.Infrastructure;

internal static class HealthCheckResponses
{
    public static HealthCheckOptions WriteJsonResponse => new()
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.ToString()
                })
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    };
}
