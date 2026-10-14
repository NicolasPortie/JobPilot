using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Playwright;
using UglyToad.PdfPig;

namespace JobPilot.Api.Services;

public static partial class ResumeExportRenderer
{
    public static string BuildHtmlDocument(string parseText, string layout)
    {
        var profile = ResumeLayoutProfile.For(layout);
        var body = BuildBodyHtml(ResumeTextSanitizer.ApplyPortugueseAccents(parseText));
        return $$"""
        <!doctype html>
        <html lang="pt-BR" xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Curriculo ATS</title>
          <style>
        {{BuildCss(profile)}}
          </style>
        </head>
        <body>
          <main class="resume-sheet resume-{{profile.Key}}">
        {{body}}
          </main>
        </body>
        </html>
        """;
    }

    public static async Task WriteTextAsync(string fullPath, string parseText)
    {
        await File.WriteAllTextAsync(fullPath, NormalizePlainText(parseText), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static async Task WriteDocxAsync(string fullPath, string parseText, string layout)
    {
        var documentXml = BuildDocxDocument(parseText, layout);
        await using var stream = File.Create(fullPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        await WriteZipEntryAsync(archive, "[Content_Types].xml", ContentTypesXml);
        await WriteZipEntryAsync(archive, "_rels/.rels", RootRelationshipsXml);
        await WriteZipEntryAsync(archive, "word/document.xml", documentXml);
        await WriteZipEntryAsync(archive, "word/styles.xml", StylesXml);
        await WriteZipEntryAsync(archive, "word/_rels/document.xml.rels", DocumentRelationshipsXml);
    }

    public static async Task WritePdfAsync(string fullPath, string parseText, string layout)
    {
        var html = BuildHtmlDocument(parseText, layout);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchChromiumAsync(playwright);
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 794, Height = 1123 },
            DeviceScaleFactor = 1
        });
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });
        await page.PdfAsync(new PagePdfOptions
        {
            Path = fullPath,
            Format = "A4",
            PrintBackground = true,
            PreferCSSPageSize = true
        });
    }

    public static int CountPdfPages(string fullPath)
    {
        using var document = PdfDocument.Open(fullPath);
        var pages = 0;
        foreach (var _ in document.GetPages()) pages++;
        return pages;
    }

    public static string ExtractPdfText(string fullPath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(fullPath);
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text ?? "");
        return sb.ToString();
    }

    public static string ExtractDocxText(string fullPath)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("DOCX exportado sem documento principal.");
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(w + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(node => node.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text));
        return string.Join(Environment.NewLine, paragraphs);
    }

    public static string NormalizeForComparison(string value) =>
        Regex.Replace(NormalizePlainText(value), @"\s+", " ").Trim();

    public static decimal CalculateTokenCoverage(string expectedText, string extractedText)
    {
        var expected = TokenSet(expectedText);
        if (expected.Count == 0) return 1m;
        var extracted = TokenSet(extractedText);
        var matched = expected.Count(token => extracted.Contains(token));
        return (decimal)matched / expected.Count;
    }

    private static async Task<IBrowser> LaunchChromiumAsync(IPlaywright playwright)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = "chrome"
            });
        }
        catch
        {
            try
            {
                return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Não foi possível iniciar o Chromium para gerar o PDF. Instale o browser do Playwright ou mantenha o Google Chrome disponível no ambiente.", ex);
            }
        }
    }

    private static string BuildBodyHtml(string parseText)
    {
        var lines = NormalizePlainText(parseText)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
        var sb = new StringBuilder();
        var inList = false;
        var emittedName = false;
        var emittedHeadline = false;
        var currentSection = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (inList)
                {
                    sb.AppendLine("</ul>");
                    inList = false;
                }
                sb.AppendLine("<div class=\"resume-space\"></div>");
                continue;
            }

            if (line.Equals("OBJETIVO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsSectionHeading(line))
            {
                if (inList)
                {
                    sb.AppendLine("</ul>");
                    inList = false;
                }
                sb.Append("<h2>").Append(Html(line)).AppendLine("</h2>");
                currentSection = line;
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!inList)
                {
                    sb.AppendLine("<ul>");
                    inList = true;
                }
                sb.Append("<li>").Append(Html(line[2..].Trim())).AppendLine("</li>");
                continue;
            }

            if (inList)
            {
                sb.AppendLine("</ul>");
                inList = false;
            }

            if (!emittedName)
            {
                sb.Append("<p class=\"name\">").Append(Html(line)).AppendLine("</p>");
                emittedName = true;
                continue;
            }

            if (!emittedHeadline && !LooksLikeContactLine(line))
            {
                sb.Append("<p class=\"headline\">").Append(Html(line)).AppendLine("</p>");
                emittedHeadline = true;
                continue;
            }

            if (LooksLikeEntryHeading(currentSection, line))
            {
                sb.Append("<p class=\"entry-heading\">").Append(Linkify(line)).AppendLine("</p>");
                continue;
            }

            if (LooksLikeContactLine(line))
            {
                sb.Append(BuildContactHtml(line));
                continue;
            }

            sb.Append("<p class=\"line\">").Append(Linkify(line)).AppendLine("</p>");
        }

        if (inList)
            sb.AppendLine("</ul>");
        return sb.ToString();
    }

    private static string BuildDocxDocument(string parseText, string layout)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var profile = ResumeLayoutProfile.For(layout);
        var body = new XElement(w + "body");
        var lines = NormalizePlainText(parseText).Split('\n');
        var lineIndex = 0;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var paragraph = new XElement(w + "p");
            var properties = new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", lineIndex == 0 ? "0" : "100")));
            if (IsSectionHeading(line))
                properties.Add(new XElement(w + "keepNext"));
            paragraph.Add(properties);

            if (!string.IsNullOrWhiteSpace(line))
            {
                var runProperties = new XElement(w + "rPr");
                if (lineIndex == 0)
                    runProperties.Add(new XElement(w + "b"));
                paragraph.Add(new XElement(w + "r", runProperties, new XElement(w + "t", PreserveXmlWhitespace(line))));
            }

            body.Add(paragraph);
            lineIndex++;
        }

        body.Add(new XElement(w + "sectPr",
            new XElement(w + "pgSz", new XAttribute(w + "w", "11906"), new XAttribute(w + "h", "16838")),
            new XElement(w + "pgMar", new XAttribute(w + "top", PageMarginTwips(profile.PageMarginIn)), new XAttribute(w + "right", PageMarginTwips(profile.PageMarginIn)), new XAttribute(w + "bottom", PageMarginTwips(profile.PageMarginIn)), new XAttribute(w + "left", PageMarginTwips(profile.PageMarginIn))),
            new XElement(w + "cols", new XAttribute(w + "num", "1"))));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString(SaveOptions.DisableFormatting);
    }

    private static string PreserveXmlWhitespace(string value) => value.StartsWith(' ') || value.EndsWith(' ') ? value : value;

    private static string PageMarginTwips(decimal inches) => Math.Max(0, (int)Math.Round(inches * 1440m)).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task WriteZipEntryAsync(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await writer.WriteAsync(content);
    }

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
        </Types>
        """;

    private const string RootRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
            <w:name w:val="Normal"/>
            <w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:sz w:val="21"/></w:rPr>
          </w:style>
        </w:styles>
        """;

    private static bool IsSectionHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        var trimmed = line.Trim();
        if (trimmed.Length > 48) return false;
        return trimmed.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || c == '/' || c == '#' || c == '&');
    }

    private static bool LooksLikeContactLine(string line) =>
        line.Contains('@') ||
        line.Contains("linkedin", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("github", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("http", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(line, @"\d{2,}.*\d{4,}");

    private static bool LooksLikeEntryHeading(string currentSection, string line)
    {
        if (!currentSection.Contains("EXPERI", StringComparison.OrdinalIgnoreCase)) return false;
        if (line.StartsWith("- ", StringComparison.Ordinal)) return false;
        return line.Contains(" - ", StringComparison.Ordinal) ||
               Regex.IsMatch(line, @"\|\s*(?:Jan|Fev|Mar|Abr|Mai|Jun|Jul|Ago|Set|Out|Nov|Dez|\d{2})", RegexOptions.IgnoreCase);
    }

    private static string BuildContactHtml(string line)
    {
        var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var links = parts.Where(part => part.Contains("http", StringComparison.OrdinalIgnoreCase)).ToArray();
        var contacts = parts.Except(links).ToArray();
        var sb = new StringBuilder();
        if (contacts.Length > 0)
            sb.Append("<p class=\"contact\">").Append(Html(string.Join(" | ", contacts))).AppendLine("</p>");
        if (links.Length > 0)
        {
            sb.Append("<p class=\"contact-links\">");
            for (var index = 0; index < links.Length; index++)
            {
                if (index > 0) sb.Append(" | ");
                var url = NormalizeDisplayUrl(links[index]);
                sb.Append("<span class=\"contact-label\">").Append(Html(LinkLabel(url))).Append(":</span> ");
                sb.Append("<a href=\"").Append(Html(EnsureAbsoluteUrl(url))).Append("\">").Append(Html(url)).Append("</a>");
            }
            sb.AppendLine("</p>");
        }
        return sb.ToString();
    }

    private static string NormalizeDisplayUrl(string value)
    {
        var url = (value ?? "").Trim().TrimEnd('.', ',', ';');
        url = Regex.Replace(url, @"^https?://(www\.)?", "", RegexOptions.IgnoreCase);
        return url.TrimEnd('/');
    }

    private static string EnsureAbsoluteUrl(string value) =>
        value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? value : $"https://{value}";

    private static string LinkLabel(string url)
    {
        if (url.Contains("linkedin", StringComparison.OrdinalIgnoreCase)) return "LinkedIn";
        if (url.Contains("github", StringComparison.OrdinalIgnoreCase)) return "GitHub";
        return "Portfolio";
    }

    private static string Linkify(string line)
    {
        var escaped = Html(line);
        return Regex.Replace(escaped, @"https?://[^\s|<]+", match =>
        {
            var url = match.Value.TrimEnd('.', ',', ';');
            var suffix = match.Value[url.Length..];
            return $"<a href=\"{url}\">{url}</a>{suffix}";
        }, RegexOptions.IgnoreCase);
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string Css(decimal value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string NormalizePlainText(string value)
    {
        var normalized = (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim() + "\n";
    }

    private static HashSet<string> TokenSet(string value) =>
        Regex.Matches(value.ToLowerInvariant(), @"\b[\p{L}\p{N}+#.]{3,}\b")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

}
