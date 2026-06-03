using System.Text;
using System.Text.Json.Serialization;
using JobPilot.Api.Data;
using JobPilot.Api.Endpoints;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddCors(options =>
{
    var allowedOrigins = (builder.Configuration["Frontend:Origins"] ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:5174,http://127.0.0.1:5174")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
                allowedOrigins.Contains(origin) ||
                origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddDbContext<JobPilotDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<AnswerResolver>();
builder.Services.AddScoped<JobFilterService>();
builder.Services.AddScoped<ProfileGuard>();
builder.Services.AddScoped<ReusableAnswerService>();
builder.Services.AddScoped<CheckpointResolutionService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AutomationProcessService>();
builder.Services.AddScoped<ApplicationAuditService>();
builder.Services.AddScoped<ApplicationLimitService>();
builder.Services.AddScoped<PlatformCredentialService>();
builder.Services.AddScoped<AgentTaskQueueService>();
builder.Services.AddScoped<AgentAnswerResolutionService>();
builder.Services.AddScoped<AiProviderService>();
builder.Services.AddScoped<ApplicationRunService>();
builder.Services.AddScoped<AutoApplyPolicyService>();
builder.Services.AddScoped<AgentLearningService>();
builder.Services.AddScoped<OperationalHealthService>();
builder.Services.AddScoped<ApplicationMaintenanceService>();
builder.Services.AddScoped<ApplicationResumeService>();
builder.Services.AddScoped<ApplicationResumePreparationService>();
builder.Services.AddScoped<ResumeFileService>();
builder.Services.AddScoped<JobTargetService>();
builder.Services.AddScoped<JobTargetSourceFileService>();
builder.Services.AddScoped<ResumeEvidenceMapService>();
builder.Services.AddScoped<ResumeTailoringService>();
builder.Services.AddScoped<ResumeExportService>();
builder.Services.AddScoped<ResumeSectionService>();
builder.Services.AddScoped<ResumeReviewAnswerService>();
builder.Services.AddSingleton<ResumeOcrService>();
builder.Services.AddSingleton<TraceRedactionService>();
builder.Services.AddSingleton<ResumeTextService>();
builder.Services.AddHttpClient<ProfileImportService>();
builder.Services.AddHttpClient<IAiChatService, AiChatService>();
builder.Services.AddHttpClient<JobTargetService>();
builder.Services.AddHostedService<RunMaintenanceService>();

var jwtKey = builder.Configuration["Auth:JwtKey"] ?? "dev-only-change-this-key-please-32";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "JobPilot",
            ValidAudience = "JobPilot",
            IssuerSigningKey = signingKey
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        context.Response.Headers.Append("Access-Control-Allow-Origin", context.Request.Headers.Origin.ToString());
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobPilotDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
        DO $$
        BEGIN
            IF to_regclass('public."Users"') IS NOT NULL AND to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );

                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20260522235433_InitialCreate', '10.0.8');
            END IF;
        END $$;
        """);
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Configuration);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "JobPilot" }));

var api = app.MapGroup("/api");

api.MapAuthEndpoints();

var authed = api.MapGroup("").RequireAuthorization();

authed.MapProfileEndpoints();
authed.MapResumeEndpoints();
authed.MapResumeEvidenceMapEndpoints();
authed.MapJobTargetEndpoints();
authed.MapSearchEndpoints();

authed.MapJobEndpoints();
authed.MapAnswerEndpoints();
authed.MapJobFilterEndpoints();
authed.MapEventEndpoints();
authed.MapHealthEndpoints();

authed.MapAiProviderEndpoints();
authed.MapPlatformCredentialEndpoints();

authed.MapAgentManagementEndpoints();
authed.MapAutomationEndpoints();
api.MapExtensionEndpoints(authed);

authed.MapRunEndpoints();
authed.MapCheckpointEndpoints();

api.MapAgentEndpoints();
authed.MapApplicationAuditEndpoints();
app.Run();
