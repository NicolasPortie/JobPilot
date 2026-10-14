using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

/// <summary>
/// Fluxo rápido: vaga (link, vários links ou texto) → requisitos → currículo adaptado, em uma chamada,
/// e histórico das candidaturas preparadas. O envio continua manual, fora do JobPilot.
/// </summary>
public sealed partial class QuickApplicationService(
    JobPilotDbContext db,
    JobTargetService targets,
    IResumeTailoringService tailoring,
    ResumeQueryService queries)
{
    private const int MaxJobsPerRequest = 15;
    public const string AppliedStatus = "applied";

    public async Task<QuickTailorResponse> TailorAsync(Guid userId, QuickTailorRequest request)
    {
        var baseId = request.BaseResumeVersionId
            ?? await queries.LatestBaseVersionIdAsync(userId)
            ?? throw new InvalidOperationException("Importe seu currículo no Perfil antes de adaptar vagas.");

        var jobs = SplitJobs(request);
        var items = new List<QuickTailorItem>();
        foreach (var job in jobs)
        {
            try
            {
                var target = await targets.OrganizeAsync(userId, job.Request);
                var tailored = await tailoring.TailorAsync(userId, new TailorResumeRequest(
                    baseId, target.Id, Layout: request.Layout ?? "ats_standard", UseAi: request.UseAi));
                items.Add(new QuickTailorItem(job.Label, true, null, target, tailored));
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
            {
                db.ChangeTracker.Clear();
                items.Add(new QuickTailorItem(job.Label, false, ex.Message, null, null));
            }
        }
        return new QuickTailorResponse(items.ToArray(), baseId);
    }

    public async Task<ApplicationHistoryItem[]> HistoryAsync(Guid userId)
    {
        var versions = await db.ResumeVersions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AtsReportJson.Contains("tailored_resume"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
        var targetIds = versions.Select(TargetIdOf).OfType<Guid>().Distinct().ToList();
        var byId = await db.JobTargets
            .AsNoTracking()
            .Where(x => x.UserId == userId && targetIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var items = versions.Select(version =>
        {
            var targetId = TargetIdOf(version);
            byId.TryGetValue(targetId ?? Guid.Empty, out var target);
            return new ApplicationHistoryItem(
                version.Id, targetId, version.TargetRole, version.TargetCompany,
                target?.Location ?? "", target?.SourceUrl ?? "", version.AtsScore,
                target?.Status == AppliedStatus, version.CreatedAt);
        }).ToList();

        // Gerar de novo o mesmo link cria outra versão: o histórico mostra a mais recente de cada vaga
        // e considera enviada se qualquer geração dela foi marcada como enviada.
        return items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.SourceUrl) ? item.ResumeVersionId.ToString() : item.SourceUrl.Trim().ToLowerInvariant())
            .Select(group => group.First() with { Applied = group.Any(item => item.Applied) })
            .ToArray();
    }

    public async Task SetAppliedAsync(Guid userId, Guid resumeVersionId, bool applied)
    {
        var version = await db.ResumeVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resumeVersionId && x.UserId == userId)
            ?? throw new KeyNotFoundException("Currículo não encontrado.");
        var targetId = TargetIdOf(version)
            ?? throw new InvalidOperationException("Este currículo não foi gerado para uma vaga.");
        var target = await db.JobTargets.FirstOrDefaultAsync(x => x.Id == targetId && x.UserId == userId)
            ?? throw new KeyNotFoundException("Vaga não encontrada.");
        target.Status = applied ? AppliedStatus : "organized";
        target.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private static Guid? TargetIdOf(Domain.ResumeVersion version)
    {
        try
        {
            using var report = JsonDocument.Parse(string.IsNullOrWhiteSpace(version.AtsReportJson) ? "{}" : version.AtsReportJson);
            return report.RootElement.ValueKind == JsonValueKind.Object &&
                   report.RootElement.TryGetProperty("jobTargetId", out var id) &&
                   id.TryGetGuid(out var guid)
                ? guid
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<(string Label, OrganizeJobTargetRequest Request)> SplitJobs(QuickTailorRequest request)
    {
        var input = (request.Input ?? "").Trim();
        if (input.Length == 0)
            throw new InvalidOperationException("Cole o link ou o texto da vaga.");

        var lines = input.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.All(line => UrlLine().IsMatch(line)))
        {
            var urls = lines.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (urls.Count > MaxJobsPerRequest)
                throw new InvalidOperationException($"Envie no máximo {MaxJobsPerRequest} links por vez.");
            return urls.Select(url => (url, new OrganizeJobTargetRequest("url", "", url, request.UseAi))).ToList();
        }

        var url = (request.JobUrl ?? "").Trim();
        return [("Texto da vaga", new OrganizeJobTargetRequest("manual_text", input, url, request.UseAi))];
    }

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlLine();
}
