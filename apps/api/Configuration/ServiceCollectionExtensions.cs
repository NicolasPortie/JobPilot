using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using JobPilot.Api.Data;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Infrastructure.Persistence;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace JobPilot.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobPilotServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi("v1", options => options.AddDocumentTransformer<LegacyOpenApiExclusion>());
        services.AddTransient<LegacyOpenApiExclusion>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ValidationFilter>();
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        services.Configure<DatabaseStartupOptions>(configuration.GetSection(DatabaseStartupOptions.SectionName));
        services.Configure<FeatureOptions>(configuration.GetSection(FeatureOptions.SectionName));

        services.AddDbContext<JobPilotDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddHealthChecks()
            .AddDbContextCheck<JobPilotDbContext>("postgresql", tags: ["ready"]);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"{httpContext.User.Identity?.Name ?? "anonymous"}:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 4
                    }));

            options.AddPolicy(RateLimitPolicies.Global, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.Agent, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    }));

            options.AddPolicy(RateLimitPolicies.Analysis, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 1
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Type = "https://jobpilot.io/errors/429",
                    Title = "Limite de requisicoes excedido",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Voce enviou muitas requisicoes. Aguarde um momento e tente novamente.",
                    Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
                };

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                    problem.Extensions["retryAfter"] = retryAfter.TotalSeconds;
                }

                await context.HttpContext.Response.WriteAsJsonAsync(problem, token);
            };
        });

        services.AddScoped<ScoringService>();
        services.AddScoped<AnswerResolver>();
        services.AddScoped<AnswerCatalogService>();
        services.AddScoped<JobFilterService>();
        services.AddScoped<ExecutionEventQueryService>();
        services.AddScoped<ProfileGuard>();
        services.AddScoped<CandidateProfileService>();
        services.AddScoped<ReusableAnswerService>();
        services.AddScoped<CheckpointResolutionService>();
        services.AddScoped<CheckpointQueryService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ReadinessService>();
        services.AddScoped<AutomationProcessService>();
        services.AddScoped<AutomationReadinessService>();
        services.AddScoped<ApplicationAuditService>();
        services.AddScoped<ApplicationLimitService>();
        services.AddScoped<IPlatformCredentialService, PlatformCredentialService>();
        services.AddScoped<AgentTaskQueueService>();
        services.AddScoped<AgentAnswerResolutionService>();
        services.AddScoped<IAgentProtocolService, AgentProtocolService>();
        services.AddScoped<AiProviderService>();
        services.AddScoped<AiSettingsService>();
        services.AddScoped<ApplicationRunService>();
        services.AddScoped<AutoApplyPolicyService>();
        services.AddScoped<AgentLearningService>();
        services.AddScoped<AgentManagementService>();
        services.AddScoped<OperationalHealthService>();
        services.AddScoped<ApplicationMaintenanceService>();
        services.AddScoped<ApplicationResumeService>();
        services.AddScoped<ApplicationResumePreparationService>();
        services.AddScoped<ResumeFileService>();
        services.AddScoped<JobTargetService>();
        services.AddScoped<JobTargetUrlExtractionService>();
        services.AddScoped<JobTargetSourceFileService>();
        services.AddScoped<ResumeEvidenceMapService>();
        services.AddScoped<IResumeTailoringService, ResumeTailoringService>();
        services.AddScoped<ResumeExportService>();
        services.AddScoped<ResumeSectionService>();
        services.AddScoped<ResumeReviewAnswerService>();
        services.AddScoped<ResumeQueryService>();
        services.AddScoped<QuickApplicationService>();
        services.AddScoped<AccountDeletionService>();
        services.AddScoped<RetentionService>();
        services.AddSingleton<ResumeOcrService>();
        services.AddSingleton<TraceRedactionService>();
        services.AddSingleton<ResumeTextService>();
        services.AddHttpClient<ProfileImportService>(client =>
            {
                client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .ConfigurePrimaryHttpMessageHandler(() => OutboundUrlPolicy.CreateHandler());
        services.AddSingleton<AiHealthTracker>();
        services.AddHttpClient<AiCompletionService>(client =>
            {
                client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
                // Gerar um currículo completo leva mais que uma resposta curta.
                client.Timeout = TimeSpan.FromSeconds(150);
            })
            .ConfigurePrimaryHttpMessageHandler(() => OutboundUrlPolicy.CreateHandler(allowLocalOllama: true));
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddHttpClient<JobTargetUrlExtractionService>(client =>
            {
                client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .ConfigurePrimaryHttpMessageHandler(() => OutboundUrlPolicy.CreateHandler());
        services.AddHostedService<RunMaintenanceService>();

        var jwtKey = SecurityConfiguration.JwtKey(configuration);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = SecurityConfiguration.Issuer,
                    ValidAudience = SecurityConfiguration.Audience,
                    IssuerSigningKey = signingKey
                };
            });
        services.AddScoped<IAuthorizationHandler, ActiveAgentDeviceHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("LegacyRuntime", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("legacy_runtime", "enabled");
            });

            options.AddPolicy("ActiveAgent", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("agent");
                policy.RequireClaim("legacy_runtime", "enabled");
                policy.AddRequirements(new ActiveAgentDeviceRequirement());
            });
        });

        return services;
    }

    public static IServiceCollection AddJobPilotCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration["Frontend:Origins"] ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.SetIsOriginAllowed(allowedOrigins.Contains)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    // O navegador só lê o nome do arquivo baixado se o cabeçalho for exposto.
                    .WithExposedHeaders("Content-Disposition")
                    .AllowCredentials());
        });

        return services;
    }
}
