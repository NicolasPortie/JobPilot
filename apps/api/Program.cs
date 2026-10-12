using JobPilot.Api.Configuration;
using JobPilot.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

SecurityConfiguration.Validate(builder.Configuration, builder.Environment);
builder.Services.AddJobPilotCors(builder.Configuration);
builder.Services.AddJobPilotServices(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

await app.InitializeDatabaseAsync();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponses.WriteJsonResponse.ResponseWriter
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponses.WriteJsonResponse.ResponseWriter
});

app.Run();

public partial class Program;
