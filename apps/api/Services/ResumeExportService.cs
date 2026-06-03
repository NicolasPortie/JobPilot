using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace JobPilot.Api.Services;

public sealed class ResumeExportService(JobPilotDbContext db, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResumeExportResponse> ExportAsync(Guid userId, Guid resumeVersionId, CreateResumeExportRequest request)
    {
        var format = NormalizeFormat(request.Format);
        if (format is not ("txt" or "docx" or "pdf"))
            throw new InvalidOperationException("Exportacao atual suporta TXT, DOCX e PDF ATS-safe.");

        var version = await db.ResumeVersions.FirstOrDefaultAsync(x => x.Id == resumeVersionId && x.UserId == userId)
            ?? throw new InvalidOperationException("Versao de curriculo nao encontrada.");
        if (string.IsNullOrWhiteSpace(version.PlainText))
            throw new InvalidOperationException("Versao de curriculo sem texto para exportar.");

        var layout = NormalizeLayout(request.Layout);
        var expectedParseText = NormalizePlainText(version.PlainText);
        var root = configuration["Candidate:ResumeExportDirectory"];
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Directory.GetCurrentDirectory(), "documents", "resume-exports");

        var userDir = Path.Combine(root, userId.ToString("N"));
        Directory.CreateDirectory(userDir);

        var safeName = MakeSafeFileName($"{version.TargetRole}-{version.TargetCompany}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        var fullPath = Path.Combine(userDir, $"{safeName}.{format}");
        if (format == "docx")
            await WriteDocxAsync(fullPath, expectedParseText, layout);
        else if (format == "pdf")
            await WritePdfAsync(fullPath, expectedParseText, layout);
        else
            await File.WriteAllTextAsync(fullPath, expectedParseText, Encoding.UTF8);

        var parseText = format == "docx"
            ? NormalizePlainText(await ExtractDocxTextAsync(fullPath))
            : format == "pdf"
                ? NormalizePlainText(ExtractPdfText(fullPath))
            : expectedParseText;
        var warnings = BuildWarnings(parseText)
            .Concat(BuildExportValidationWarnings(format, expectedParseText, parseText))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var export = new ResumeExport
        {
            Id = Guid.NewGuid(),
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

    public async Task<ResumeExport?> GetAsync(Guid userId, Guid exportId) =>
        await db.ResumeExports.FirstOrDefaultAsync(x => x.Id == exportId && x.UserId == userId);

    private static ResumeExportResponse ToResponse(ResumeExport export) =>
        new(export.Id, export.ResumeVersionId, export.Format, export.FilePath, ParseWarnings(export.ParseWarningsJson), export.CreatedAt);

    private static string NormalizeFormat(string value) =>
        (value ?? "").Trim().ToLowerInvariant() switch
        {
            "" => "txt",
            "text" => "txt",
            "plain_text" => "txt",
            "word" => "docx",
            "document" => "docx",
            "portable_document" => "pdf",
            var format => format
        };

    private static string NormalizeLayout(string value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant().Replace("-", "_");
        return normalized is "ats_reverse_chronological" or "ats_classic" or "ats_compact" or "ats_skills_first" or "ats_modern_minimal" or "human_review" ? normalized : "ats_reverse_chronological";
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

        if (format == "docx" && !NormalizeForComparison(expectedText).Equals(NormalizeForComparison(extractedText), StringComparison.OrdinalIgnoreCase))
            warnings.Add("Texto reextraido do DOCX difere do preview; revise a ordem de leitura antes de enviar para ATS.");
        if (format == "pdf" && CalculateTokenCoverage(expectedText, extractedText) < 0.6m)
            warnings.Add("Texto reextraido do PDF perdeu muitos termos do preview; revise antes de enviar para ATS.");

        return warnings.ToArray();
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

    private static async Task WriteDocxAsync(string fullPath, string parseText, string layout)
    {
        await using var stream = File.Create(fullPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        await WriteZipEntryAsync(archive, "[Content_Types].xml", ContentTypesXml);
        await WriteZipEntryAsync(archive, "_rels/.rels", PackageRelationshipsXml);
        await WriteZipEntryAsync(archive, "word/_rels/document.xml.rels", DocumentRelationshipsXml);
        await WriteZipEntryAsync(archive, "word/document.xml", BuildDocumentXml(parseText, layout));
    }

    private static async Task WritePdfAsync(string fullPath, string parseText, string layout)
    {
        var pages = BuildPdfPages(parseText, layout);
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"
        };

        var pageObjectNumbers = Enumerable.Range(0, pages.Count).Select(index => 3 + index * 2).ToArray();
        objects.Add($"<< /Type /Pages /Kids [{string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"))}] /Count {pages.Count} >>");

        var fontObjectNumber = 3 + pages.Count * 2;
        var boldFontObjectNumber = fontObjectNumber + 1;
        var fontDescriptorObjectNumber = fontObjectNumber + 2;
        var boldFontDescriptorObjectNumber = fontObjectNumber + 3;
        var cidFontObjectNumber = fontObjectNumber + 4;
        var boldCidFontObjectNumber = fontObjectNumber + 5;
        var toUnicodeObjectNumber = fontObjectNumber + 6;
        foreach (var (content, index) in pages.Select((content, index) => (content, index)))
        {
            var pageObjectNumber = 3 + index * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontObjectNumber} 0 R /F2 {boldFontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        objects.Add(BuildType0FontObject("JobPilotSans", cidFontObjectNumber, toUnicodeObjectNumber));
        objects.Add(BuildType0FontObject("JobPilotSans-Bold", boldCidFontObjectNumber, toUnicodeObjectNumber));
        objects.Add(BuildFontDescriptorObject("JobPilotSans", false));
        objects.Add(BuildFontDescriptorObject("JobPilotSans-Bold", true));
        objects.Add(BuildCidFontObject("JobPilotSans", fontDescriptorObjectNumber));
        objects.Add(BuildCidFontObject("JobPilotSans-Bold", boldFontDescriptorObjectNumber));
        objects.Add(BuildIdentityToUnicodeCMapObject());

        await using var stream = File.Create(fullPath);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: false);
        await writer.WriteLineAsync("%PDF-1.4");
        await writer.WriteLineAsync("% JobPilot ATS-safe resume export");

        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            await writer.FlushAsync();
            offsets.Add(stream.Position);
            await writer.WriteLineAsync($"{i + 1} 0 obj");
            await writer.WriteLineAsync(objects[i]);
            await writer.WriteLineAsync("endobj");
        }

        await writer.FlushAsync();
        var xrefOffset = stream.Position;
        await writer.WriteLineAsync("xref");
        await writer.WriteLineAsync($"0 {objects.Count + 1}");
        await writer.WriteLineAsync("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
            await writer.WriteLineAsync($"{offset:0000000000} 00000 n ");
        await writer.WriteLineAsync("trailer");
        await writer.WriteLineAsync($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        await writer.WriteLineAsync("startxref");
        await writer.WriteLineAsync(xrefOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await writer.WriteLineAsync("%%EOF");
    }

    private static List<string> BuildPdfPages(string parseText, string layout)
    {
        var isHumanReview = layout == "human_review";
        var maxLinesPerPage = layout switch
        {
            "ats_compact" => 58,
            "ats_modern_minimal" => 52,
            _ => 52
        };
        var fontSize = layout switch
        {
            "ats_compact" => 9,
            "human_review" => 11,
            _ => 10
        };
        var wrapChars = layout == "ats_compact" ? 100 : isHumanReview ? 85 : 92;
        var lines = NormalizePlainText(parseText)
            .Split('\n')
            .SelectMany(line => WrapPdfLine(line.TrimEnd(), wrapChars))
            .ToList();
        if (lines.Count == 0) lines.Add("");

        var pages = new List<string>();
        foreach (var chunk in lines.Chunk(maxLinesPerPage))
            pages.Add(BuildPdfContentStream(chunk, fontSize, isHumanReview));
        return pages;
    }

    private static string BuildPdfContentStream(IEnumerable<string> lines, int fontSize, bool isHumanReview = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BT");
        var lineHeight = isHumanReview ? 16 : 14;
        var startY = isHumanReview ? 720 : 740;
        var startX = isHumanReview ? 90 : 72;
        sb.AppendLine($"/F1 {fontSize} Tf");
        sb.AppendLine($"{lineHeight} TL");
        sb.AppendLine($"{startX} {startY} Td");
        foreach (var line in lines)
        {
            if (IsSectionHeading(line))
            {
                sb.AppendLine($"/F2 {fontSize + 1} Tf");
                sb.AppendLine("0.12 0.25 0.45 rg");
                sb.Append(EncodePdfUnicodeString(line)).AppendLine(" Tj");
                sb.AppendLine("0 0 0 rg");
                sb.AppendLine($"/F1 {fontSize} Tf");
            }
            else
            {
                sb.Append(EncodePdfUnicodeString(line)).AppendLine(" Tj");
            }
            sb.AppendLine("T*");
        }
        sb.Append("ET");
        return sb.ToString();
    }

    private static bool IsSectionHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        var trimmed = line.Trim();
        return trimmed.Length <= 40 && trimmed.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || c == '/' || c == '#' || c == '&');
    }

    private static IEnumerable<string> WrapPdfLine(string line, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield return "";
            yield break;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > maxChars)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (word.Length > maxChars)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                for (var index = 0; index < word.Length; index += maxChars)
                    yield return word.Substring(index, Math.Min(maxChars, word.Length - index));
                continue;
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0) yield return current.ToString();
    }

    private static string ExtractPdfText(string fullPath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(fullPath);
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text ?? "");
        return sb.ToString();
    }

    private static async Task<string> ExtractDocxTextAsync(string fullPath)
    {
        await using var stream = File.OpenRead(fullPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null) return "";

        await using var documentStream = entry.Open();
        var document = await XDocument.LoadAsync(documentStream, LoadOptions.None, CancellationToken.None);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(w + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(text => text.Value)));
        return string.Join('\n', paragraphs);
    }

    private static async Task WriteZipEntryAsync(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(content);
    }

    private static string BuildDocumentXml(string parseText, string layout)
    {
        var isHumanReview = layout == "human_review";
        var spacing = layout switch
        {
            "ats_compact" => "120",
            "ats_modern_minimal" => "160",
            "human_review" => "200",
            _ => "160"
        };
        var fontSize = layout switch
        {
            "ats_compact" => "20",
            "ats_modern_minimal" => "22",
            "human_review" => "24",
            _ => "22"
        };
        var fontName = isHumanReview ? "Calibri" : "Arial";
        var margins = isHumanReview ? "1080" : "720";
        var paragraphs = NormalizePlainText(parseText)
            .Split('\n')
            .Select(line => line.Trim())
            .Select(line => BuildParagraphXml(line, spacing, fontSize, fontName, isHumanReview && IsSectionHeading(line)));

        return $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body>
        {{string.Join("\n", paragraphs)}}
            <w:sectPr>
              <w:pgSz w:w="12240" w:h="15840"/>
              <w:pgMar w:top="{{margins}}" w:right="{{margins}}" w:bottom="{{margins}}" w:left="{{margins}}" w:header="0" w:footer="0" w:gutter="0"/>
              <w:cols w:num="1" w:space="720"/>
              <w:docGrid w:linePitch="360"/>
            </w:sectPr>
          </w:body>
        </w:document>
        """;
    }

    private static string BuildParagraphXml(string line, string spacing, string fontSize, string fontName, bool isBold)
    {
        if (string.IsNullOrWhiteSpace(line))
            return $$"""
                <w:p>
                  <w:pPr><w:spacing w:after="{{spacing}}"/></w:pPr>
                </w:p>
            """;

        var escaped = EscapeXml(line);
        var boldOpen = isBold ? "<w:b/>" : "";
        var color = isBold ? "<w:color w:val=\"2B579A\"/>" : "";
        return $$"""
            <w:p>
              <w:pPr><w:spacing w:after="{{spacing}}"/><w:rPr><w:rFonts w:ascii="{{fontName}}" w:hAnsi="{{fontName}}"/><w:sz w:val="{{fontSize}}"/>{{boldOpen}}{{color}}</w:rPr></w:pPr>
              <w:r><w:rPr><w:rFonts w:ascii="{{fontName}}" w:hAnsi="{{fontName}}"/><w:sz w:val="{{fontSize}}"/>{{boldOpen}}{{color}}</w:rPr><w:t xml:space="preserve">{{escaped}}</w:t></w:r>
            </w:p>
        """;
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string EncodePdfUnicodeString(string value)
    {
        var normalized = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
        var bytes = Encoding.BigEndianUnicode.GetBytes(normalized);
        return $"<{Convert.ToHexString(bytes)}>";
    }

    private static string BuildType0FontObject(string baseFont, int cidFontObjectNumber, int toUnicodeObjectNumber) =>
        $"<< /Type /Font /Subtype /Type0 /BaseFont /{baseFont} /Encoding /Identity-H /DescendantFonts [{cidFontObjectNumber} 0 R] /ToUnicode {toUnicodeObjectNumber} 0 R >>";

    private static string BuildCidFontObject(string baseFont, int descriptorObjectNumber) =>
        $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{baseFont} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptorObjectNumber} 0 R /DW 500 /W [32 [250] 33 126 500 160 255 500 256 65535 500] >>";

    private static string BuildFontDescriptorObject(string fontName, bool bold)
    {
        var flags = bold ? 262176 : 32;
        var stemV = bold ? 120 : 80;
        return $"<< /Type /FontDescriptor /FontName /{fontName} /Flags {flags} /FontBBox [-665 -325 2000 1040] /ItalicAngle 0 /Ascent 905 /Descent -212 /CapHeight 715 /StemV {stemV} >>";
    }

    private static string BuildIdentityToUnicodeCMapObject()
    {
        const string cmap = """
            /CIDInit /ProcSet findresource begin
            12 dict begin
            begincmap
            /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
            /CMapName /JobPilot-Identity-UCS def
            /CMapType 2 def
            1 begincodespacerange
            <0000> <FFFF>
            endcodespacerange
            1 beginbfrange
            <0000> <FFFF> <0000>
            endbfrange
            endcmap
            CMapName currentdict /CMap defineresource pop
            end
            end
            """;
        return $"<< /Length {Encoding.ASCII.GetByteCount(cmap)} >>\nstream\n{cmap}\nendstream";
    }

    private static string NormalizeForComparison(string value) =>
        Regex.Replace(NormalizePlainText(value), @"\s+", " ").Trim();

    private static decimal CalculateTokenCoverage(string expectedText, string extractedText)
    {
        var expected = TokenSet(expectedText);
        if (expected.Count == 0) return 1m;
        var extracted = TokenSet(extractedText);
        var matched = expected.Count(token => extracted.Contains(token));
        return (decimal)matched / expected.Count;
    }

    private static HashSet<string> TokenSet(string value) =>
        Regex.Matches(value.ToLowerInvariant(), @"\b[\p{L}\p{N}+#.]{3,}\b")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
        </Types>
        """;

    private const string PackageRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
        """;
}
