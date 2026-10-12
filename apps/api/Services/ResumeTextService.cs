using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobPilot.Api.Domain;
using UglyToad.PdfPig;

namespace JobPilot.Api.Services;

public class ResumeTextService
{
    private const long MaxInputBytes = 10 * 1024 * 1024;
    private const int MaxPdfPages = 40;
    private const long MaxDocxUncompressedBytes = 25 * 1024 * 1024;
    private const long MaxDocxEntryBytes = 8 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string ReadResumeText(string? resumePath) => ReadResume(resumePath).Text;

    public ResumeTextExtraction ReadResume(string? resumePath)
    {
        if (string.IsNullOrWhiteSpace(resumePath) || !File.Exists(resumePath))
        {
            return new ResumeTextExtraction("", EmptyReport("missing", "Arquivo de currículo não encontrado."));
        }

        var info = new FileInfo(resumePath);
        if (info.Length > MaxInputBytes)
            return new ResumeTextExtraction("", EmptyReport("oversized", "Arquivo de currículo maior que 10 MB."));
        var cacheKey = info.FullName;
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.LastWriteUtc == info.LastWriteTimeUtc)
            return cached.Extraction;

        var extraction = Extract(info);
        _cache[cacheKey] = new CacheEntry(info.LastWriteTimeUtc, extraction);
        return extraction;
    }

    private static ResumeTextExtraction Extract(FileInfo file)
    {
        var sourceKind = file.Extension.TrimStart('.').ToLowerInvariant();
        try
        {
            var raw = file.Extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtractPdfText(file.FullName),
                ".docx" => ExtractDocx(file.FullName),
                ".txt" or ".md" => new RawExtraction(File.ReadAllText(file.FullName), "plain_text", 1, 1),
                _ => new RawExtraction("", "unsupported", 0, 0)
            };

            var text = NormalizeWhitespace(raw.Text);
            text = text.Length > 12000 ? text[..12000] : text;
            return new ResumeTextExtraction(text, BuildReport(sourceKind, raw, text));
        }
        catch
        {
            return new ResumeTextExtraction("", EmptyReport(sourceKind, "Falha ao extrair texto do arquivo."));
        }
    }

    private static RawExtraction ExtractPdfText(string path)
    {
        var sb = new StringBuilder();
        var pages = 0;
        var pagesWithText = 0;

        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            pages++;
            if (pages > MaxPdfPages)
                throw new InvalidDataException($"PDF excede o limite de {MaxPdfPages} paginas.");
            var pageText = page.Text ?? "";
            if (CountWords(pageText) >= 5) pagesWithText++;
            sb.AppendLine(pageText);

            var links = page.GetHyperlinks()
                .Select(link => link.Uri?.Trim())
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (links.Length > 0)
            {
                sb.AppendLine("PDF hyperlinks:");
                foreach (var link in links)
                {
                    sb.AppendLine(link);
                }
            }
        }

        return new RawExtraction(sb.ToString(), "native_pdf", pages, pagesWithText);
    }

    private static RawExtraction ExtractDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var totalUncompressedBytes = 0L;
        foreach (var archiveEntry in archive.Entries)
        {
            if (archiveEntry.Length > MaxDocxEntryBytes ||
                totalUncompressedBytes > MaxDocxUncompressedBytes - archiveEntry.Length)
                throw new InvalidDataException("DOCX excede o limite de descompactacao permitido.");
            totalUncompressedBytes += archiveEntry.Length;
        }
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null) return new RawExtraction("", "native_docx", 1, 0);

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(w + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(node => node.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text));
        var text = string.Join(Environment.NewLine, paragraphs);
        return new RawExtraction(text, "native_docx", 1, string.IsNullOrWhiteSpace(text) ? 0 : 1);
    }

    private static ResumeExtractionReport BuildReport(string sourceKind, RawExtraction raw, string text)
    {
        var warnings = new List<string>();
        var characterCount = text.Length;
        var wordCount = CountWords(text);
        var alphaNumeric = text.Count(char.IsLetterOrDigit);
        var strangeChars = text.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch) && !char.IsPunctuation(ch) && !char.IsSymbol(ch));
        var strangeRatio = characterCount == 0 ? 0m : (decimal)strangeChars / characterCount;
        var hasEmail = Regex.IsMatch(text, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        var hasPhone = Regex.IsMatch(text, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}");
        var hasSection = Regex.IsMatch(text, @"\b(experi[eê]ncia|form[açc][aã]o|educa[cç][aã]o|skills|habilidades|projetos|certifica[cç][oõ]es)\b", RegexOptions.IgnoreCase);

        var score = 0;
        if (characterCount >= 500) score += 25;
        if (characterCount >= 1500) score += 15;
        if (wordCount >= 120) score += 15;
        if (raw.Pages == 0 || raw.PagesWithNativeText == raw.Pages) score += 15;
        else if (raw.PagesWithNativeText > 0) score += 8;
        if (hasEmail) score += 8;
        if (hasPhone) score += 6;
        if (hasSection) score += 10;
        if (characterCount > 0 && alphaNumeric > characterCount * 0.55) score += 6;
        if (strangeRatio > 0.03m) score -= 15;
        score = Math.Clamp(score, 0, 100);
        if (sourceKind == "pdf" && raw.Method == "native_pdf" && score > 92)
            score = 92;

        if (characterCount < 300) warnings.Add("Texto extraído muito curto; o arquivo pode ser escaneado, imagem ou ter camada de texto ruim.");
        if (sourceKind == "pdf" && raw.Pages > 0 && raw.PagesWithNativeText == 0) warnings.Add("PDF sem texto nativo detectável; OCR é necessário antes de preencher automaticamente.");
        if (sourceKind == "pdf" && raw.Pages > 0 && raw.PagesWithNativeText < raw.Pages) warnings.Add("PDF misto: algumas páginas não possuem texto nativo suficiente.");
        if (!hasEmail) warnings.Add("E-mail não encontrado no texto extraído.");
        if (!hasPhone) warnings.Add("Telefone não encontrado no texto extraído.");
        if (!hasSection) warnings.Add("Seções comuns de currículo não foram detectadas com confiança.");
        if (strangeRatio > 0.03m) warnings.Add("Muitos caracteres incomuns detectados; pode haver problema de encoding ou OCR.");

        var requiresOcr = sourceKind == "pdf" && (raw.PagesWithNativeText == 0 || characterCount < 300);
        var requiresReview = requiresOcr || score < 60 || warnings.Count >= 3;
        var classification = requiresOcr
            ? "image_only_or_low_text"
            : sourceKind == "pdf" && raw.Pages > 0 && raw.PagesWithNativeText < raw.Pages
                ? "mixed_native_and_image"
                : score >= 75
                    ? "native_text_ok"
                    : "native_text_suspicious";

        return new ResumeExtractionReport(
            sourceKind,
            raw.Method,
            classification,
            score,
            Math.Clamp(score / 100m, 0m, 1m),
            requiresOcr,
            requiresReview,
            raw.Pages,
            raw.PagesWithNativeText,
            characterCount,
            wordCount,
            warnings.ToArray());
    }

    private static ResumeExtractionReport EmptyReport(string sourceKind, string warning) =>
        new(sourceKind, "none", "unreadable", 0, 0m, true, true, 0, 0, 0, 0, [warning]);

    private static string NormalizeWhitespace(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static int CountWords(string value) => Regex.Matches(value ?? "", @"\b[\p{L}\p{N}+#.]+\b").Count;

    private sealed record RawExtraction(string Text, string Method, int Pages, int PagesWithNativeText);
    private sealed record CacheEntry(DateTime LastWriteUtc, ResumeTextExtraction Extraction);
}

public sealed record ResumeTextExtraction(string Text, ResumeExtractionReport Report);
