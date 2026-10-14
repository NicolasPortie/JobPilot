using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ResumeExportService(JobPilotDbContext db, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeExportResponse> ExportAsync(Guid userId, Guid resumeVersionId, CreateResumeExportRequest request)
    {
        var format = NormalizeFormat(request.Format);
        if (format is not ("txt" or "docx" or "pdf"))
            throw new InvalidOperationException("Exportação atual suporta TXT, DOCX e PDF.");

        var version = await db.ResumeVersions.FirstOrDefaultAsync(x => x.Id == resumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");
        if (string.IsNullOrWhiteSpace(version.PlainText))
            throw new InvalidOperationException("Versão de currículo sem texto para exportar.");

        var layout = NormalizeLayout(request.Layout);
        var expectedParseText = NormalizePlainText(ResumeTextSanitizer.ApplyPortugueseAccents(version.PlainText));
        if (expectedParseText.Length > 30000)
            throw new InvalidOperationException("Versão de currículo longa demais para exportar (limite de 30000 caracteres).");
        var root = configuration["Candidate:ResumeExportDirectory"];
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Directory.GetCurrentDirectory(), "documents", "resume-exports");

        var userDir = Path.Combine(root, userId.ToString("N"));
        Directory.CreateDirectory(userDir);

        var extension = format;
        var exportId = Guid.NewGuid();
        var safeName = MakeSafeFileName($"{version.TargetRole}-{version.TargetCompany}");
        var fullPath = Path.Combine(userDir, $"{safeName}-{exportId:N}.{extension}");
        switch (format)
        {
            case "txt":
                await ResumeExportRenderer.WriteTextAsync(fullPath, expectedParseText);
                break;
            case "docx":
                await ResumeExportRenderer.WriteDocxAsync(fullPath, expectedParseText, layout);
                break;
            default:
                await ResumeExportRenderer.WritePdfAsync(fullPath, expectedParseText, layout);
                break;
        }

        var parseText = format switch
        {
            "pdf" => NormalizePlainText(ResumeExportRenderer.ExtractPdfText(fullPath)),
            "docx" => NormalizePlainText(ResumeExportRenderer.ExtractDocxText(fullPath)),
            _ => expectedParseText,
        };
        var warnings = BuildWarnings(parseText)
            .Concat(BuildExportValidationWarnings(format, expectedParseText, parseText))
            .Concat(BuildPageCountWarnings(format, fullPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var export = new ResumeExport
        {
            Id = exportId,
            UserId = userId,
            ResumeVersionId = version.Id,
            Format = format,
            FilePath = fullPath,
            ParseText = parseText,
            ParseWarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ResumeExports.Add(export);
        await db.SaveChangesAsync();
        return ToResponse(export);
    }

    public async Task<ResumePreviewResponse> PreviewAsync(Guid userId, Guid resumeVersionId, CreateResumeExportRequest request)
    {
        var version = await db.ResumeVersions.FirstOrDefaultAsync(x => x.Id == resumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versão de currículo não encontrada.");
        if (string.IsNullOrWhiteSpace(version.PlainText))
            throw new InvalidOperationException("Versão de currículo sem texto para gerar prévia.");

        var layout = NormalizeLayout(request.Layout);
        var html = ResumeExportRenderer.BuildHtmlDocument(NormalizePlainText(ResumeTextSanitizer.ApplyPortugueseAccents(version.PlainText)), layout);
        return new ResumePreviewResponse(version.Id, layout, html);
    }

    public async Task<ResumeExport?> GetAsync(Guid userId, Guid exportId) =>
        await db.ResumeExports.FirstOrDefaultAsync(x => x.Id == exportId && x.UserId == userId);

    /// <summary>Nome do arquivo baixado: Curriculo-Nome-Sobrenome-Cargo-Empresa.ext (o arquivo em disco leva um GUID).</summary>
    public async Task<string> DownloadFileNameAsync(Guid userId, ResumeExport export)
    {
        var version = await db.ResumeVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == export.ResumeVersionId && x.UserId == userId);
        var fullName = (await db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId))?.FullName ?? "";
        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2);
        var parts = new[] { "Curriculo", string.Join(" ", nameParts), version?.TargetRole ?? "", version?.TargetCompany ?? "" }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var baseName = MakeSafeFileName(string.Join(" ", parts));
        var extension = Path.GetExtension(export.FilePath);
        return baseName + extension;
    }

    private static ResumeExportResponse ToResponse(ResumeExport export) =>
        new(export.Id, export.ResumeVersionId, export.Format, ParseWarnings(export.ParseWarningsJson), export.CreatedAt);

    private static string NormalizeFormat(string value) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "" => "pdf",
            "txt" or "text" or "plain_text" => "txt",
            "doc" or "docx" or "word" or "document" => "docx",
            "portable_document" => "pdf",
            var format => format
        };

    private static string NormalizeLayout(string value)
    {
        return ResumeTextSanitizer.NormalizeLayout(value);
    }

    private static string NormalizePlainText(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim() + "\n";
    }

    private static string[] BuildWarnings(string parseText)
    {
        var warnings = new List<string>();
        if (parseText.Length < 600) warnings.Add("Texto exportado curto; revise se o curriculo tem experiencia suficiente.");
        if (Regex.IsMatch(parseText, @"[│┌┐└┘■●◆★]")) warnings.Add("Texto contem simbolos decorativos; revise antes de enviar para ATS.");
        if (parseText.Contains('\t')) warnings.Add("Texto contem tabulacao; exportacao removeu excesso, mas revise alinhamento.");
        if (Regex.Matches(parseText, @"\b[\p{L}\p{N}+#.]+\b").Count > 900) warnings.Add("Texto longo; considere uma versao mais concisa.");
        return warnings.ToArray();
    }

    private static string[] BuildExportValidationWarnings(string format, string expectedText, string extractedText)
    {
        if (format is not ("docx" or "pdf")) return [];

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            warnings.Add($"Nao foi possivel reextrair texto do {format.ToUpperInvariant()} exportado; revise antes de enviar para ATS.");
            return warnings.ToArray();
        }

        if (format is "pdf" or "docx" && ResumeExportRenderer.CalculateTokenCoverage(expectedText, extractedText) < 0.6m)
            warnings.Add($"Texto reextraido do {format.ToUpperInvariant()} perdeu muitos termos do preview; revise antes de enviar para ATS.");

        return warnings.ToArray();
    }

    private static string[] BuildPageCountWarnings(string format, string fullPath)
    {
        if (!format.Equals("pdf", StringComparison.OrdinalIgnoreCase)) return [];
        try
        {
            var pages = ResumeExportRenderer.CountPdfPages(fullPath);
            return pages > 5
                ? [$"PDF exportado com {pages} páginas; ATS e recrutadores preferem 1 a 2 páginas."]
                : [];
        }
        catch
        {
            return [];
        }
    }
    private static string[] ParseWarnings(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static string MakeSafeFileName(string value)
    {
        var chars = (value ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var collapsed = Regex.Replace(new string(chars), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "curriculo-ats" : collapsed[..Math.Min(collapsed.Length, 90)];
    }

}
