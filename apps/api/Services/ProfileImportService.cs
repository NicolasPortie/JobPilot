using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobPilot.Api.Data;
using JobPilot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPilot.Api.Services;

public sealed class ProfileImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly JobPilotDbContext _db;
    private readonly ResumeTextService _resumeText;
    private readonly ResumeOcrService _ocr;
    private readonly IAiChatService _ai;
    private readonly HttpClient _http;
    private readonly Func<string, ResumeOcrResult> _runOcr;

    [ActivatorUtilitiesConstructor]
    public ProfileImportService(JobPilotDbContext db, ResumeTextService resumeText, ResumeOcrService ocr, IAiChatService ai, HttpClient http)
        : this(db, resumeText, ocr, ai, http, ocr.RunOcr)
    {
    }

    public ProfileImportService(JobPilotDbContext db, ResumeTextService resumeText, ResumeOcrService ocr, IAiChatService ai, HttpClient http, Func<string, ResumeOcrResult> runOcr)
    {
        _db = db;
        _resumeText = resumeText;
        _ocr = ocr;
        _ai = ai;
        _http = http;
        _runOcr = runOcr;
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public Task<ProfileImportResponse> ImportAsync(Guid userId, ProfileImportRequest request) =>
        ImportCoreAsync(userId, request, null);

    /// <summary>Reprocessa o último arquivo enviado com o parser atual, sem novo upload.</summary>
    public async Task<ProfileImportResponse> ReimportStoredAsync(Guid userId, bool useAi)
    {
        var path = (await _db.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId))?.ResumePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("Nenhum currículo enviado foi encontrado. Envie o arquivo novamente.");
        return await ImportCoreAsync(userId, new ProfileImportRequest(false, useAi), path);
    }

    public Task<ProfileImportResponse> ImportStoredFileAsync(Guid userId, string storedResumePath, bool fetchPublicLinks, bool useAi) =>
        ImportCoreAsync(userId, new ProfileImportRequest(fetchPublicLinks, useAi), storedResumePath);

    private async Task<ProfileImportResponse> ImportCoreAsync(Guid userId, ProfileImportRequest request, string? storedResumePath)
    {
        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new CandidateProfile { Id = Guid.NewGuid(), UserId = userId };
            _db.CandidateProfiles.Add(profile);
        }

        var resumePath = string.IsNullOrWhiteSpace(storedResumePath) ? profile.ResumePath : storedResumePath.Trim();
        if (!string.IsNullOrWhiteSpace(resumePath)) profile.ResumePath = resumePath;

        var warnings = new List<string>();
        var extraction = _resumeText.ReadResume(resumePath);
        var effectiveExtraction = extraction;
        var resumeText = extraction.Text;

        // Auto-run OCR if needed and available
        if (extraction.Report.RequiresOcr && !string.IsNullOrWhiteSpace(resumePath) && File.Exists(resumePath))
        {
            var ocrResult = _runOcr(resumePath);
            if (ocrResult.Success && ocrResult.WordCount > extraction.Report.WordCount)
            {
                resumeText = ocrResult.Text;
                effectiveExtraction = BuildOcrExtraction(extraction, ocrResult);
                warnings.AddRange(effectiveExtraction.Report.Warnings);
            }
            else if (ocrResult.Warnings.Length > 0)
            {
                warnings.AddRange(ocrResult.Warnings);
            }
        }

        if (ReferenceEquals(effectiveExtraction, extraction))
            warnings.AddRange(extraction.Report.Warnings);
        if (string.IsNullOrWhiteSpace(resumeText))
            warnings.Add("Não foi possível extrair texto do currículo informado.");
        if (effectiveExtraction.Report.RequiresOcr && !warnings.Any(w => w.Contains("OCR executado")))
            warnings.Add("OCR necessário: a extração nativa não tem confiança suficiente para importar automaticamente.");
        if (effectiveExtraction.Report.RequiresReview)
            warnings.Add("Revise os campos importados antes de usar este currículo em candidaturas.");

        var links = ResumeProfileInference.ExtractLinks($"{resumeText} {profile.LinkedinUrl} {profile.GithubUrl} {profile.PortfolioUrl}");
        var fetched = request.FetchPublicLinks ? await FetchPublicLinkTextAsync(links, warnings) : new Dictionary<string, string>();
        var publicText = string.Join("\n\n", fetched.Select(x => $"URL: {x.Key}\n{x.Value}"));

        var deterministic = ResumeProfileInference.BuildDeterministicImport(resumeText, publicText, profile);
        ResumeProfileImportData importData = deterministic;
        if (request.UseAi)
        {
            try
            {
                var aiData = await _ai.AnalyzeResumeProfileAsync(userId, resumeText, publicText, profile);
                if (aiData is not null)
                    importData = ResumeProfileInference.MergeImportData(deterministic, aiData);
                else
                    warnings.Add("IA indisponivel ou sem JSON valido; importacao deterministica usada.");
            }
            catch
            {
                warnings.Add("IA falhou durante a importacao; importacao deterministica usada.");
            }
        }

        var previousSkills = new HashSet<string>(profile.Skills, StringComparer.OrdinalIgnoreCase);
        ApplyImport(profile, importData, resumePath);
        await PersistResumeLibraryAsync(userId, resumePath, effectiveExtraction, importData, resumeText);
        await _db.SaveChangesAsync();

        var addedSkills = profile.Skills
            .Where(skill => !previousSkills.Contains(skill))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var currentSkills = new HashSet<string>(profile.Skills, StringComparer.OrdinalIgnoreCase);
        var droppedSkills = previousSkills.Where(skill => !currentSkills.Contains(skill)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        if (droppedSkills.Length > 0)
            warnings.Add($"Competências do perfil que o currículo não cita foram removidas: {string.Join(", ", droppedSkills)}. Adicione de volta no Perfil só o que você pode comprovar.");

        return new ProfileImportResponse(profile, addedSkills, fetched.Keys.ToArray(), warnings.Distinct().ToArray(), effectiveExtraction.Report, resumeText, importData);
    }

    private static ResumeTextExtraction BuildOcrExtraction(ResumeTextExtraction nativeExtraction, ResumeOcrResult ocrResult)
    {
        var text = ocrResult.Text.Trim();
        var wordCount = CountWords(text);
        var characterCount = text.Length;
        var qualityScore = CalculateTextQualityScore(text, wordCount);
        var requiresReview = qualityScore < 60 || wordCount < 50;
        var warnings = nativeExtraction.Report.Warnings
            .Where(w => !w.Contains("OCR necessario", StringComparison.OrdinalIgnoreCase))
            .Concat([$"OCR executado: {wordCount} palavras extraidas."])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var method = string.IsNullOrWhiteSpace(nativeExtraction.Report.ExtractionMethod) || nativeExtraction.Report.ExtractionMethod == "unsupported"
            ? "ocr"
            : $"{nativeExtraction.Report.ExtractionMethod}+ocr";

        var report = new ResumeExtractionReport(
            nativeExtraction.Report.SourceKind,
            method,
            requiresReview ? "ocr_needs_review" : "ocr_text",
            qualityScore,
            Math.Clamp(qualityScore / 100m, 0m, 1m),
            false,
            requiresReview,
            nativeExtraction.Report.Pages,
            nativeExtraction.Report.PagesWithNativeText,
            characterCount,
            wordCount,
            warnings);

        return new ResumeTextExtraction(text, report);
    }

    private static int CountWords(string value) =>
        Regex.Matches(value ?? "", @"\b[\p{L}\p{N}+#.]{2,}\b").Count;

    private static int CalculateTextQualityScore(string text, int wordCount)
    {
        var score = 20;
        if (wordCount >= 350) score += 45;
        else if (wordCount >= 150) score += 35;
        else if (wordCount >= 50) score += 20;
        else if (wordCount >= 20) score += 10;

        if (Regex.IsMatch(text, @"[\w.+-]+@[\w.-]+\.[a-z]{2,}", RegexOptions.IgnoreCase)) score += 10;
        if (Regex.IsMatch(text, @"\+?\d[\d\s().-]{7,}")) score += 10;
        if (Regex.IsMatch(text, @"experi[eê]ncia|experience|forma[cç][aã]o|education|skills|compet[eê]ncias", RegexOptions.IgnoreCase)) score += 15;
        return Math.Clamp(score, 0, 100);
    }

    private async Task PersistResumeLibraryAsync(Guid userId, string resumePath, ResumeTextExtraction extraction, ResumeProfileImportData importData, string finalText)
    {
        var now = DateTimeOffset.UtcNow;
        var fileName = string.IsNullOrWhiteSpace(resumePath) ? "" : Path.GetFileName(resumePath);
        var import = new ResumeImport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceFileName = fileName,
            SourcePath = resumePath,
            SourceKind = extraction.Report.SourceKind,
            ExtractionMethod = extraction.Report.ExtractionMethod,
            Classification = extraction.Report.Classification,
            TextQualityScore = extraction.Report.TextQualityScore,
            Confidence = extraction.Report.Confidence,
            RequiresOcr = extraction.Report.RequiresOcr,
            RequiresReview = extraction.Report.RequiresReview,
            Pages = extraction.Report.Pages,
            PagesWithNativeText = extraction.Report.PagesWithNativeText,
            CharacterCount = extraction.Report.CharacterCount,
            WordCount = extraction.Report.WordCount,
            RawText = finalText,
            WarningsJson = JsonSerializer.Serialize(extraction.Report.Warnings, JsonOptions),
            CreatedAt = now
        };

        var document = new ResumeDocument
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeImportId = import.Id,
            Title = string.IsNullOrWhiteSpace(importData.TargetRole) ? "Currículo base" : $"Currículo base - {importData.TargetRole}",
            SourceFileName = fileName,
            SourceKind = extraction.Report.SourceKind,
            Status = extraction.Report.RequiresReview ? "needs_review" : "ready",
            CreatedAt = now,
            UpdatedAt = now
        };

        var plainText = ResumeImportSectionBuilder.BuildPlainResume(importData, finalText);
        var version = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = document.Id,
            Title = ResumeQueryService.BaseVersionTitle,
            TargetRole = importData.TargetRole,
            Status = extraction.Report.RequiresReview ? "needs_review" : "draft",
            AtsScore = extraction.Report.TextQualityScore,
            AtsReportJson = JsonSerializer.Serialize(extraction.Report, JsonOptions),
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };

        version.Sections.AddRange(ResumeImportSectionBuilder.BuildSections(userId, version.Id, importData, finalText));
        _db.ResumeImports.Add(import);
        _db.ResumeDocuments.Add(document);
        _db.ResumeVersions.Add(version);
    }

    private static void ApplyImport(CandidateProfile profile, ResumeProfileImportData data, string resumePath)
    {
        profile.FullName = ResumeProfileInference.Prefer(profile.FullName, data.FullName);
        profile.Email = ResumeProfileInference.Prefer(profile.Email, data.Email);
        profile.Phone = ResumeProfileInference.Prefer(profile.Phone, data.Phone);
        profile.City = ResumeProfileInference.Prefer(profile.City, data.City);
        profile.State = ResumeProfileInference.Prefer(profile.State, data.State);
        profile.Country = ResumeProfileInference.Prefer(profile.Country, data.Country, "Brasil");
        profile.LinkedinUrl = ResumeProfileInference.Prefer(profile.LinkedinUrl, data.LinkedinUrl);
        profile.GithubUrl = ResumeProfileInference.Prefer(profile.GithubUrl, data.GithubUrl);
        profile.PortfolioUrl = ResumeProfileInference.Prefer(profile.PortfolioUrl, data.PortfolioUrl);
        profile.TargetRole = ResumeProfileInference.Prefer(profile.TargetRole, data.TargetRole);
        profile.Seniority = ResumeProfileInference.Prefer(profile.Seniority, data.Seniority, "unknown").ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(data.ProfessionalSummary) &&
            (string.IsNullOrWhiteSpace(profile.ProfessionalSummary) ||
             ResumeProfileInference.IsPollutedSummary(profile.ProfessionalSummary) ||
             data.ProfessionalSummary.Length > profile.ProfessionalSummary.Length / 2))
            profile.ProfessionalSummary = data.ProfessionalSummary.Trim();
        // O perfil espelha o currículo importado: competências antigas que o arquivo não cita não passam a constar como fato.
        if (data.Skills.Length > 0) profile.Skills = ResumeProfileInference.MergeSkills([], data.Skills);
        if (!string.IsNullOrWhiteSpace(resumePath)) profile.ResumePath = resumePath;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<Dictionary<string, string>> FetchPublicLinkTextAsync(IEnumerable<string> links, List<string> warnings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(links.Where(IsFetchablePublicLink).Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("JobPilotProfileImporter/1.0");

        while (queue.Count > 0 && result.Count < 10)
        {
            var link = queue.Dequeue();
            if (!seen.Add(link)) continue;
            try
            {
                var githubProfile = await FetchGithubProfileTextAsync(link);
                if (!string.IsNullOrWhiteSpace(githubProfile))
                    result[$"{link}#github-public-repos"] = githubProfile;

                using var response = await _http.GetAsync(link);
                if (!response.IsSuccessStatusCode) continue;
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase))
                    continue;
                var body = await response.Content.ReadAsStringAsync();
                result[link] = HtmlToText(body);
                if (Uri.TryCreate(link, UriKind.Absolute, out var currentUri) &&
                    currentUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var discovered in ResumeProfileInference.ExtractLinks(body).Where(IsFetchablePublicLink).Where(IsUsefulProfileLink))
                {
                    if (!seen.Contains(discovered) && !queue.Contains(discovered, StringComparer.OrdinalIgnoreCase))
                        queue.Enqueue(discovered);
                }
            }
            catch
            {
                warnings.Add($"Não foi possível ler link público: {link}");
            }
        }
        return result;
    }

    private static bool IsFetchablePublicLink(string link) =>
        OutboundUrlPolicy.TryValidatePublicHttpUrl(link, out _);

    private static bool IsUsefulProfileLink(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return segments.Length == 1;
        if (OutboundUrlPolicy.IsHostOrSubdomain(host, "linkedin.com"))
            return segments.Length >= 2 && segments[0].Equals("in", StringComparison.OrdinalIgnoreCase);
        return OutboundUrlPolicy.IsHostOrSubdomain(host, "nicolasportie.com");
    }

    private async Task<string> FetchGithubProfileTextAsync(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return "";

        var user = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(user) || user.Contains('.')) return "";

        try
        {
            using var response = await _http.GetAsync($"https://api.github.com/users/{user}/repos?per_page=100&sort=updated");
            if (!response.IsSuccessStatusCode) return "";
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var parts = new List<string> { $"GitHub profile: {user}" };
            foreach (var repo in json.RootElement.EnumerateArray().Take(60))
            {
                var name = repo.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "";
                var language = repo.TryGetProperty("language", out var langProp) ? langProp.GetString() : "";
                var description = repo.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";
                var topics = repo.TryGetProperty("topics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", topicsProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                    : "";
                parts.Add($"Repository {name}. Language {language}. Topics {topics}. Description {description}.");
            }
            return string.Join("\n", parts);
        }
        catch
        {
            return "";
        }
    }

    private static string HtmlToText(string html)
    {
        var noScript = Regex.Replace(html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var text = Regex.Replace(noScript, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 6000 ? text[..6000] : text;
    }

}
