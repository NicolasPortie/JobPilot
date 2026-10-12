using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JobPilot.Api.Infrastructure;

public sealed class LegacyOpenApiExclusion : IOpenApiDocumentTransformer
{
    private static readonly string[] ExcludedPrefixes =
    [
        "/api/agent",
        "/api/ai/",
        "/api/answers",
        "/api/application-jobs",
        "/api/automation",
        "/api/checkpoints",
        "/api/events",
        "/api/health/operational",
        "/api/platform-credentials",
        "/api/question-patterns",
        "/api/runs",
        "/api/system",
    ];

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var path in document.Paths.Keys.ToArray())
        {
            if (ExcludedPrefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                (path.Length == prefix.Length || path[prefix.Length] == '/')))
            {
                document.Paths.Remove(path);
            }
        }

        return Task.CompletedTask;
    }
}
