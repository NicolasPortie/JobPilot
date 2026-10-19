using System.Text;
using System.Text.Json;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class JobTargetService(JobPilotDbContext db, IAiChatService ai, ResumeOcrService ocr, JobTargetUrlExtractionService urlExtraction)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<JobTargetDetail> OrganizeAsync(Guid userId, OrganizeJobTargetRequest request, string? storedImagePath = null)
    {
        var source = await ResolveSourceAsync(userId, request, storedImagePath);
        if ((string.IsNullOrWhiteSpace(source.Text) || source.Text.Trim().Length < 80) && source.Attachments.Length == 0)
            throw new InvalidOperationException("Texto da vaga insuficiente para organizar requisitos.");

        JobRequirementExtractionResponse? extraction = null;
        if (request.UseAi)
        {
            var input = new JobRequirementExtractionInput(
                source.Text,
                source.Url,
                source.Title,
                source.Company,
                source.Location,
                source.Attachments);
            extraction = source.Attachments.Length > 0
                ? await ai.ExtractJobRequirementsFromAttachmentsAsync(userId, input, source.Attachments)
                : await ai.ExtractJobRequirementsAsync(userId, input);
        }

        extraction ??= JobTargetAnalyzer.BuildDeterministicExtraction(source);
        extraction = JobTargetAnalyzer.EnrichWithDeterministicRequirements(extraction, source);
        extraction = JobTargetAnalyzer.NormalizeExtraction(extraction, source);
        if (extraction.Requirements.Length == 0)
            throw new InvalidOperationException(source.Kind == "url"
                ? "Não consegui ler esta vaga pelo link (o site bloqueou ou a página não tem a descrição). Cole o texto da vaga."
                : "Não encontrei requisitos neste texto. Cole a descrição completa da vaga.");

        var now = DateTimeOffset.UtcNow;
        var target = new JobTarget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceKind = source.Kind,
            SourceText = source.Text,
            SourceUrl = source.Url,
            Title = extraction.Title,
            Company = extraction.Company,
            Location = extraction.Location,
            Seniority = extraction.Seniority,
            Workplace = extraction.Workplace,
            Summary = extraction.Summary,
            Status = extraction.Warnings.Length > 0 || extraction.Confidence < 0.65m ? "needs_review" : "organized",
            Confidence = extraction.Confidence,
            WarningsJson = JsonSerializer.Serialize(extraction.Warnings, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now
        };

        var requirements = extraction.Requirements
            .Select((item, index) => ToRequirement(userId, target.Id, item, index + 1))
            .Where(item => !string.IsNullOrWhiteSpace(item.Label))
            .ToList();
        target.Requirements.AddRange(requirements);
        db.JobTargets.Add(target);
        await db.SaveChangesAsync();

        return ToDetail(target);
    }

    public async Task<List<JobTargetDetail>> ListAsync(Guid userId)
    {
        var targets = await db.JobTargets
            .Include(x => x.Requirements)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToListAsync();
        return targets.Select(ToDetail).ToList();
    }

    public async Task<JobTargetDetail?> GetAsync(Guid userId, Guid id)
    {
        var target = await db.JobTargets
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        return target is null ? null : ToDetail(target);
    }

    public Task<JobTargetDetail> OrganizeStoredImageAsync(Guid userId, string storedPath, bool useAi) =>
        OrganizeAsync(userId, new OrganizeJobTargetRequest("image_ocr", null, null, useAi), storedPath);

    private async Task<ResolvedJobSource> ResolveSourceAsync(Guid userId, OrganizeJobTargetRequest request, string? storedImagePath = null)
    {
        var kind = NormalizeSourceKind(request.SourceKind);
        if (kind == "image_ocr")
        {
            if (string.IsNullOrWhiteSpace(storedImagePath) ||
                !IsTrustedImagePath(userId, storedImagePath) ||
                !File.Exists(storedImagePath))
                throw new InvalidOperationException("Imagem não encontrada ou caminho inválido.");
            var ocrResult = ocr.RunOcr(storedImagePath);
            var attachment = BuildAttachment(storedImagePath);
            if (string.IsNullOrWhiteSpace(ocrResult.Text) && !request.UseAi)
                throw new InvalidOperationException("Não foi possível extrair texto da imagem.");
            var sourceText = string.IsNullOrWhiteSpace(ocrResult.Text)
                ? "Screenshot/PDF de vaga enviado pelo usuario. Extraia os requisitos somente do anexo visual."
                : ocrResult.Text;
            return new ResolvedJobSource("image_ocr", sourceText, "", "", "", "", attachment is null ? [] : [attachment], []);
        }

        if (kind == "url")
        {
            var url = request.Url?.Trim() ?? "";
            var manualText = request.Text?.Trim() ?? "";
            return await urlExtraction.ResolveAsync(url, manualText);
        }

        return new ResolvedJobSource(
            kind,
            request.Text?.Trim() ?? "",
            request.Url?.Trim() ?? "",
            "",
            "",
            "",
            [],
            []);
    }

    private static AiInputAttachment? BuildAttachment(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var mediaType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".pdf" => "application/pdf",
            _ => ""
        };
        if (string.IsNullOrWhiteSpace(mediaType)) return null;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Anexo de vaga maior que o limite permitido.");
        var bytes = File.ReadAllBytes(path);
        var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        var type = mediaType == "application/pdf" ? "file" : "image_url";
        return new AiInputAttachment(type, mediaType, dataUrl, Path.GetFileName(path));
    }

    private static bool IsTrustedImagePath(Guid userId, string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var userSegment = userId.ToString("N");
            var roots = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "documents", "job-screenshots", userSegment),
                Path.Combine(Directory.GetCurrentDirectory(), "storage", "job-target-sources", userSegment)
            };

            return roots.Any(root =>
            {
                var fullRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            });
        }
        catch
        {
            return false;
        }
    }

    private static JobRequirement ToRequirement(Guid userId, Guid targetId, JobRequirementItem item, int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobTargetId = targetId,
            Category = item.Category,
            Label = item.Label,
            Importance = item.Importance,
            EvidenceText = item.EvidenceText,
            NormalizedKeyword = item.NormalizedKeyword,
            ExactPhrases = item.ExactPhrases ?? [],
            SortOrder = order
        };

    private static JobTargetDetail ToDetail(JobTarget target)
    {
        var warnings = ParseStringArray(target.WarningsJson);
        return new JobTargetDetail(
            target.Id,
            target.SourceKind,
            target.SourceUrl,
            target.Title,
            target.Company,
            target.Location,
            target.Seniority,
            target.Workplace,
            target.Summary,
            target.Status,
            target.Confidence,
            warnings,
            target.Requirements
                .OrderBy(x => x.SortOrder)
                .Select(x => new JobRequirementDto(x.Id, x.Category, x.Label, x.Importance, x.EvidenceText, x.NormalizedKeyword, x.ExactPhrases, x.SortOrder))
                .ToArray(),
            target.UpdatedAt);
    }

    private static string[] ParseStringArray(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static string NormalizeSourceKind(string value) =>
        NormalizeOneOf(value, ["manual_text", "url", "image_ocr"], "manual_text");

    private static string NormalizeOneOf(string value, string[] allowed, string fallback)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : fallback;
    }

}
