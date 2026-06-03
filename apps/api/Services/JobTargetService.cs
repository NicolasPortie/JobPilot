using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class JobTargetService(JobPilotDbContext db, IAiChatService ai, HttpClient http, ResumeOcrService ocr)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedScreenshotExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".pdf" };
    private static readonly string[] KnownTech =
    [
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "SQL", "PostgreSQL", "MySQL",
        "JavaScript", "TypeScript", "React", "Angular", "Vue", "Node.js", "Java",
        "Spring Boot", "API REST", "REST", "Docker", "Kubernetes", "Git", "GitHub",
        "Azure", "AWS", "GCP", "Selenium", "Playwright", "QA", "Testes"
    ];

    public async Task<JobTargetDetail> OrganizeAsync(Guid userId, OrganizeJobTargetRequest request)
    {
        var source = await ResolveSourceAsync(userId, request);
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

        extraction ??= BuildDeterministicExtraction(source);
        extraction = NormalizeExtraction(extraction, source);

        var now = DateTimeOffset.UtcNow;
        var target = new JobTarget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobLeadId = source.JobLeadId,
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

    public async Task<JobTargetDetail> OrganizeScreenshotAsync(Guid userId, IFormFile file, bool useAi)
    {
        if (file.Length <= 0) throw new InvalidOperationException("Arquivo vazio.");
        if (file.Length > 12 * 1024 * 1024) throw new InvalidOperationException("Screenshot maior que 12MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedScreenshotExtensions.Contains(extension))
            throw new InvalidOperationException("Formato nao suportado. Use PNG, JPG, WEBP, TIFF, BMP ou PDF.");

        var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "documents", "job-screenshots");
        var userDir = Path.Combine(uploadRoot, userId.ToString("N"));
        Directory.CreateDirectory(userDir);

        var safeName = MakeSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{safeName}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(userDir, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return await OrganizeAsync(userId, new OrganizeJobTargetRequest(null, "image_ocr", null, null, fullPath, useAi));
    }

    private async Task<ResolvedJobSource> ResolveSourceAsync(Guid userId, OrganizeJobTargetRequest request)
    {
        if (request.JobLeadId is { } jobLeadId)
        {
            var lead = await db.JobLeads.FirstOrDefaultAsync(x => x.Id == jobLeadId && x.UserId == userId)
                ?? throw new InvalidOperationException("Vaga nao encontrada.");
            return new ResolvedJobSource(
                "job_lead",
                $"{lead.Title}\n{lead.Company}\n{lead.Location}\n{lead.Description}",
                lead.Url,
                lead.Title,
                lead.Company,
                lead.Location,
                lead.Id,
                []);
        }

        var kind = NormalizeSourceKind(request.SourceKind);
        if (kind == "image_ocr")
        {
            if (string.IsNullOrWhiteSpace(request.ImagePath) || !File.Exists(request.ImagePath))
                throw new InvalidOperationException("Imagem nao encontrada ou caminho invalido.");
            var ocrResult = ocr.RunOcr(request.ImagePath);
            var attachment = BuildAttachment(request.ImagePath);
            if (string.IsNullOrWhiteSpace(ocrResult.Text) && !request.UseAi)
                throw new InvalidOperationException("Nao foi possivel extrair texto da imagem.");
            var sourceText = string.IsNullOrWhiteSpace(ocrResult.Text)
                ? "Screenshot/PDF de vaga enviado pelo usuario. Extraia os requisitos somente do anexo visual."
                : ocrResult.Text;
            return new ResolvedJobSource("image_ocr", sourceText, "", "", "", "", null, attachment is null ? [] : [attachment]);
        }

        if (kind == "url")
        {
            var url = request.Url?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("URL da vaga nao informada.");
            var html = await http.GetStringAsync(url);
            var text = ExtractTextFromHtml(html);
            if (string.IsNullOrWhiteSpace(text) || text.Length < 80)
                throw new InvalidOperationException("Nao foi possivel extrair texto suficiente da URL.");
            return new ResolvedJobSource("url", text, url, "", "", "", null, []);
        }

        return new ResolvedJobSource(
            kind,
            request.Text?.Trim() ?? "",
            request.Url?.Trim() ?? "",
            "",
            "",
            "",
            null,
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
        var bytes = File.ReadAllBytes(path);
        var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        var type = mediaType == "application/pdf" ? "file" : "image_url";
        return new AiInputAttachment(type, mediaType, dataUrl, Path.GetFileName(path));
    }

    private static string MakeSafeFileName(string value)
    {
        var chars = (value ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var collapsed = Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "vaga" : collapsed[..Math.Min(collapsed.Length, 80)];
    }

    private static string ExtractTextFromHtml(string html)
    {
        // Simple heuristic HTML-to-text extraction
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<script.*?</script>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<style.*?</style>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, "&nbsp;", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "&amp;", "&", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "&lt;", "<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "&gt;", ">", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ");
        return text.Trim();
    }

    private static JobRequirementExtractionResponse BuildDeterministicExtraction(ResolvedJobSource source)
    {
        var text = source.Text;
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        var requirements = new List<JobRequirementItem>();
        var order = 0;

        foreach (var tech in KnownTech.Where(tech => ContainsTerm(normalized, tech)))
        {
            requirements.Add(new JobRequirementItem(
                "technology",
                tech,
                LooksMustHave(normalized, tech) ? "must_have" : "nice_to_have",
                EvidenceAround(text, tech),
                NormalizeKeyword(tech),
                [tech]));
            order++;
        }

        if (ContainsAny(normalized, ["api", "apis", "rest", "backend"]))
            requirements.Add(new JobRequirementItem("responsibility", "Desenvolvimento de APIs e integrações", "context", EvidenceAround(text, "API"), "apis-rest", ["APIs", "REST"]));
        if (ContainsAny(normalized, ["testes", "testing", "qa", "qualidade"]))
            requirements.Add(new JobRequirementItem("responsibility", "Testes e qualidade de software", "context", EvidenceAround(text, "testes"), "testes", ["testes", "QA"]));

        var warnings = new List<string>();
        if (requirements.Count == 0) warnings.Add("Poucos requisitos detectados sem IA; revise o texto da vaga.");
        if (string.IsNullOrWhiteSpace(source.Title)) warnings.Add("Cargo nao foi informado explicitamente no cadastro manual.");

        return new JobRequirementExtractionResponse(
            source.Title,
            source.Company,
            source.Location,
            DetectSeniority(normalized),
            DetectWorkplace(normalized),
            BuildSummary(text),
            requirements.DistinctBy(x => x.NormalizedKeyword).Take(30).ToArray(),
            warnings.ToArray(),
            requirements.Count > 0 ? 0.55m : 0.25m);
    }

    private static JobRequirementExtractionResponse NormalizeExtraction(JobRequirementExtractionResponse extraction, ResolvedJobSource source)
    {
        var requirements = extraction.Requirements
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => x with
            {
                Category = NormalizeOneOf(x.Category, ["technology", "responsibility", "experience", "education", "language", "certification", "domain", "soft_skill", "work_model", "other"], "other"),
                Importance = NormalizeOneOf(x.Importance, ["must_have", "nice_to_have", "context"], "context"),
                Label = x.Label.Trim(),
                EvidenceText = x.EvidenceText.Trim(),
                NormalizedKeyword = string.IsNullOrWhiteSpace(x.NormalizedKeyword) ? NormalizeKeyword(x.Label) : NormalizeKeyword(x.NormalizedKeyword),
                ExactPhrases = (x.ExactPhrases ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray()
            })
            .DistinctBy(x => $"{x.Category}:{x.NormalizedKeyword}")
            .Take(50)
            .ToArray();

        var warnings = (extraction.Warnings ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
        if (requirements.Any(x => string.IsNullOrWhiteSpace(x.EvidenceText)))
            warnings.Add("Alguns requisitos vieram sem evidencia textual; revise antes de gerar curriculo.");

        return extraction with
        {
            Title = FirstNonEmpty(extraction.Title, source.Title),
            Company = FirstNonEmpty(extraction.Company, source.Company),
            Location = FirstNonEmpty(extraction.Location, source.Location),
            Seniority = NormalizeOneOf(extraction.Seniority, ["intern", "junior", "mid", "senior", "lead", "unknown"], DetectSeniority(RemoveDiacritics(source.Text).ToLowerInvariant())),
            Workplace = NormalizeOneOf(extraction.Workplace, ["remote", "hybrid", "onsite", "unknown"], DetectWorkplace(RemoveDiacritics(source.Text).ToLowerInvariant())),
            Summary = string.IsNullOrWhiteSpace(extraction.Summary) ? BuildSummary(source.Text) : extraction.Summary.Trim(),
            Requirements = requirements,
            Warnings = warnings.ToArray(),
            Confidence = Math.Clamp(extraction.Confidence, 0m, 1m)
        };
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
            target.JobLeadId,
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
        NormalizeOneOf(value, ["manual_text", "job_lead", "url", "image_ocr"], "manual_text");

    private static string NormalizeOneOf(string value, string[] allowed, string fallback)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return allowed.Contains(normalized) ? normalized : fallback;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";

    private static bool LooksMustHave(string normalizedText, string keyword)
    {
        var normalizedKeyword = RemoveDiacritics(keyword).ToLowerInvariant();
        var index = normalizedText.IndexOf(normalizedKeyword, StringComparison.Ordinal);
        if (index < 0) return false;
        var start = Math.Max(0, index - 160);
        var window = normalizedText[start..Math.Min(normalizedText.Length, index + normalizedKeyword.Length + 160)];
        return ContainsAny(window, ["obrigatorio", "obrigatoria", "necessario", "necessaria", "requisitos", "must", "required", "experiencia com"]);
    }

    private static string EvidenceAround(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalizedText = RemoveDiacritics(text).ToLowerInvariant();
        var normalizedTerm = RemoveDiacritics(term).ToLowerInvariant();
        var index = normalizedText.IndexOf(normalizedTerm, StringComparison.Ordinal);
        if (index < 0) return text[..Math.Min(text.Length, 180)];
        var start = Math.Max(0, index - 90);
        var end = Math.Min(text.Length, index + term.Length + 120);
        return text[start..end].Trim();
    }

    private static string BuildSummary(string text)
    {
        var cleaned = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return cleaned.Length <= 420 ? cleaned : cleaned[..420].Trim();
    }

    private static string DetectSeniority(string normalized)
    {
        if (ContainsAny(normalized, ["estagio", "estagiario", "intern"])) return "intern";
        if (ContainsAny(normalized, ["junior", "jr", "júnior"])) return "junior";
        if (ContainsAny(normalized, ["pleno", "mid-level", "mid level"])) return "mid";
        if (ContainsAny(normalized, ["senior", "sênior", "sr.", "sr "])) return "senior";
        if (ContainsAny(normalized, ["lead", "principal", "staff", "manager", "coordenador"])) return "lead";
        return "unknown";
    }

    private static string DetectWorkplace(string normalized)
    {
        if (ContainsAny(normalized, ["remoto", "remota", "remote", "home office", "work from home"])) return "remote";
        if (ContainsAny(normalized, ["hibrido", "híbrido", "hybrid"])) return "hybrid";
        if (ContainsAny(normalized, ["presencial", "onsite", "on-site"])) return "onsite";
        return "unknown";
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms) => terms.Any(term => ContainsTerm(value, term));

    private static bool ContainsTerm(string normalizedHaystack, string term)
    {
        var normalizedTerm = RemoveDiacritics(term.ToLowerInvariant()).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;
        if (normalizedTerm.Length <= 3 || normalizedTerm.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            var pattern = $@"(^|[^a-z0-9]){Regex.Escape(normalizedTerm)}([^a-z0-9]|$)";
            return Regex.IsMatch(normalizedHaystack, pattern, RegexOptions.IgnoreCase);
        }
        return normalizedHaystack.Contains(normalizedTerm);
    }

    private static string NormalizeKeyword(string value)
    {
        var normalized = RemoveDiacritics(value.ToLowerInvariant()).Trim();
        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "requisito" : normalized;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private sealed record ResolvedJobSource(string Kind, string Text, string Url, string Title, string Company, string Location, Guid? JobLeadId, AiInputAttachment[] Attachments);
}
