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

    public async Task<ProfileImportResponse> ImportAsync(Guid userId, ProfileImportRequest request)
    {
        var profile = await _db.CandidateProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            profile = new CandidateProfile { Id = Guid.NewGuid(), UserId = userId };
            _db.CandidateProfiles.Add(profile);
        }

        var resumePath = string.IsNullOrWhiteSpace(request.ResumePath) ? profile.ResumePath : request.ResumePath.Trim();
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
            warnings.Add("Nao foi possivel extrair texto do curriculo informado.");
        if (effectiveExtraction.Report.RequiresOcr && !warnings.Any(w => w.Contains("OCR executado")))
            warnings.Add("OCR necessario: a extracao nativa nao tem confianca suficiente para importar automaticamente.");
        if (effectiveExtraction.Report.RequiresReview)
            warnings.Add("Revise os campos importados antes de usar este curriculo em candidaturas.");

        var links = ExtractLinks($"{resumeText} {profile.LinkedinUrl} {profile.GithubUrl} {profile.PortfolioUrl}");
        var fetched = request.FetchPublicLinks ? await FetchPublicLinkTextAsync(links, warnings) : new Dictionary<string, string>();
        var publicText = string.Join("\n\n", fetched.Select(x => $"URL: {x.Key}\n{x.Value}"));

        var deterministic = BuildDeterministicImport(resumeText, publicText, profile);
        ResumeProfileImportData importData = deterministic;
        if (request.UseAi)
        {
            try
            {
                var aiData = await _ai.AnalyzeResumeProfileAsync(userId, resumeText, publicText, profile);
                if (aiData is not null)
                    importData = MergeImportData(deterministic, aiData);
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
        var answers = await UpsertAnswersAsync(userId, importData.Answers);
        await PersistResumeLibraryAsync(userId, resumePath, effectiveExtraction, importData, resumeText);
        await _db.SaveChangesAsync();

        var addedSkills = profile.Skills
            .Where(skill => !previousSkills.Contains(skill))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProfileImportResponse(profile, answers, addedSkills, fetched.Keys.ToArray(), warnings.Distinct().ToArray(), effectiveExtraction.Report, resumeText, importData);
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
            Title = string.IsNullOrWhiteSpace(importData.TargetRole) ? "Curriculo base" : $"Curriculo base - {importData.TargetRole}",
            SourceFileName = fileName,
            SourceKind = extraction.Report.SourceKind,
            Status = extraction.Report.RequiresReview ? "needs_review" : "ready",
            CreatedAt = now,
            UpdatedAt = now
        };

        var plainText = BuildPlainResume(importData, finalText);
        var version = new ResumeVersion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeDocumentId = document.Id,
            Title = "Versao base importada",
            TargetRole = importData.TargetRole,
            Status = extraction.Report.RequiresReview ? "needs_review" : "draft",
            AtsScore = extraction.Report.TextQualityScore,
            AtsReportJson = JsonSerializer.Serialize(extraction.Report, JsonOptions),
            PlainText = plainText,
            CreatedAt = now,
            UpdatedAt = now
        };

        version.Sections.AddRange(BuildSections(userId, version.Id, importData, finalText));
        _db.ResumeImports.Add(import);
        _db.ResumeDocuments.Add(document);
        _db.ResumeVersions.Add(version);
    }

    private static List<ResumeSection> BuildSections(Guid userId, Guid versionId, ResumeProfileImportData data, string rawText)
    {
        var sections = new List<ResumeSection>();
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new { data.FullName, data.Email, data.Phone, data.City, data.Country, data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl });
        AddSection(sections, userId, versionId, "summary", "Resumo", 20, new { text = data.ProfessionalSummary });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { items = data.Skills });

        // Try to extract structured sections from raw text
        var experiences = ExtractExperienceBlocks(rawText);
        if (experiences.Count > 0)
            AddSection(sections, userId, versionId, "experience", "Experiencia", 40, new { items = experiences });

        var projects = ExtractProjectBlocks(rawText);
        if (projects.Count > 0)
            AddSection(sections, userId, versionId, "projects", "Projetos", 50, new { items = projects });

        var education = ExtractEducationBlocks(rawText);
        if (education.Count > 0)
            AddSection(sections, userId, versionId, "education", "Educacao", 60, new { items = education });

        var certifications = ExtractCertificationBlocks(rawText);
        if (certifications.Count > 0)
            AddSection(sections, userId, versionId, "certifications", "Certificacoes", 70, new { items = certifications });

        var languages = ExtractLanguageBlocks(rawText);
        if (languages.Count > 0)
            AddSection(sections, userId, versionId, "languages", "Idiomas", 75, new { items = languages });

        AddSection(sections, userId, versionId, "raw_import", "Texto importado", 90, new { text = rawText });
        return sections;
    }

    private static List<Dictionary<string, string>> ExtractExperienceBlocks(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var inExperience = false;
        var currentBlock = new Dictionary<string, string>();
        var currentDescription = new List<string>();

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("experiencia") || lower.Contains("experiência") || lower.Contains("professional experience") || lower.Contains("work experience"))
            {
                inExperience = true;
                continue;
            }
            if (inExperience && (lower.Contains("educacao") || lower.Contains("educação") || lower.Contains("formacao") || lower.Contains("formação") || lower.Contains("projects") || lower.Contains("projetos") || lower.Contains("skills") || lower.Contains("certificações") || lower.Contains("certificacoes") || lower.Contains("idiomas") || lower.Contains("languages")))
            {
                if (currentBlock.Count > 0)
                {
                    if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
                    blocks.Add(currentBlock);
                }
                inExperience = false;
                currentBlock = new Dictionary<string, string>();
                currentDescription = new List<string>();
                continue;
            }

            if (!inExperience) continue;

            // Heuristic: lines with dates often indicate a job entry
            if (Regex.IsMatch(line, @"\b(20\d{2}|19\d{2})\b") && (line.Contains("-") || line.Contains("–") || line.Contains("até") || line.Contains("presente") || line.Contains("atual")))
            {
                if (currentBlock.Count > 0)
                {
                    if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
                    blocks.Add(currentBlock);
                    currentBlock = new Dictionary<string, string>();
                    currentDescription = new List<string>();
                }
                currentBlock["period"] = line;
            }
            else if (currentBlock.Count > 0 && (line.StartsWith("-") || line.StartsWith("•") || line.StartsWith("*")))
            {
                currentDescription.Add(line.TrimStart('-', '•', '*', ' '));
            }
            else if (currentBlock.Count > 0 && currentBlock.ContainsKey("period") && !currentBlock.ContainsKey("title"))
            {
                currentBlock["title"] = line;
            }
            else if (currentBlock.Count > 0 && currentBlock.ContainsKey("title") && !currentBlock.ContainsKey("company"))
            {
                currentBlock["company"] = line;
            }
            else if (currentBlock.Count > 0)
            {
                currentDescription.Add(line);
            }
        }

        if (currentBlock.Count > 0)
        {
            if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
            blocks.Add(currentBlock);
        }

        return blocks;
    }

    private static List<Dictionary<string, string>> ExtractProjectBlocks(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var inProjects = false;
        var currentBlock = new Dictionary<string, string>();
        var currentDescription = new List<string>();

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("projetos") || lower.Contains("projects"))
            {
                inProjects = true;
                continue;
            }
            if (inProjects && (lower.Contains("educacao") || lower.Contains("educação") || lower.Contains("formacao") || lower.Contains("formação") || lower.Contains("experiencia") || lower.Contains("experiência") || lower.Contains("skills") || lower.Contains("certificações") || lower.Contains("certificacoes") || lower.Contains("idiomas") || lower.Contains("languages")))
            {
                if (currentBlock.Count > 0)
                {
                    if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
                    blocks.Add(currentBlock);
                }
                inProjects = false;
                currentBlock = new Dictionary<string, string>();
                currentDescription = new List<string>();
                continue;
            }

            if (!inProjects) continue;

            if (line.StartsWith("-") || line.StartsWith("•") || line.StartsWith("*"))
            {
                if (currentBlock.Count > 0)
                {
                    if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
                    blocks.Add(currentBlock);
                    currentBlock = new Dictionary<string, string>();
                    currentDescription = new List<string>();
                }
                currentBlock["title"] = line.TrimStart('-', '•', '*', ' ');
            }
            else if (currentBlock.Count > 0)
            {
                currentDescription.Add(line);
            }
        }

        if (currentBlock.Count > 0)
        {
            if (currentDescription.Count > 0) currentBlock["description"] = string.Join("\n", currentDescription);
            blocks.Add(currentBlock);
        }

        return blocks;
    }

    private static List<Dictionary<string, string>> ExtractEducationBlocks(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var inEducation = false;
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("educacao") || lower.Contains("educação") || lower.Contains("formacao") || lower.Contains("formação") || lower.Contains("education"))
            {
                inEducation = true;
                continue;
            }
            if (inEducation && (lower.Contains("experiencia") || lower.Contains("experiência") || lower.Contains("projetos") || lower.Contains("projects") || lower.Contains("skills") || lower.Contains("certificações") || lower.Contains("certificacoes") || lower.Contains("idiomas") || lower.Contains("languages")))
            {
                inEducation = false;
                continue;
            }
            if (!inEducation) continue;
            if (line.Length > 5)
                blocks.Add(new Dictionary<string, string> { ["description"] = line });
        }
        return blocks;
    }

    private static List<Dictionary<string, string>> ExtractCertificationBlocks(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var inCerts = false;
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("certificações") || lower.Contains("certificacoes") || lower.Contains("certifications") || lower.Contains("certs"))
            {
                inCerts = true;
                continue;
            }
            if (inCerts && (lower.Contains("experiencia") || lower.Contains("experiência") || lower.Contains("projetos") || lower.Contains("projects") || lower.Contains("skills") || lower.Contains("educacao") || lower.Contains("educação") || lower.Contains("idiomas") || lower.Contains("languages")))
            {
                inCerts = false;
                continue;
            }
            if (!inCerts) continue;
            if (line.Length > 3)
                blocks.Add(new Dictionary<string, string> { ["description"] = line });
        }
        return blocks;
    }

    private static List<Dictionary<string, string>> ExtractLanguageBlocks(string text)
    {
        var blocks = new List<Dictionary<string, string>>();
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var inLangs = false;
        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (lower.Contains("idiomas") || lower.Contains("languages") || lower.Contains("language"))
            {
                inLangs = true;
                continue;
            }
            if (inLangs && (lower.Contains("experiencia") || lower.Contains("experiência") || lower.Contains("projetos") || lower.Contains("projects") || lower.Contains("skills") || lower.Contains("educacao") || lower.Contains("educação") || lower.Contains("certificações") || lower.Contains("certificacoes")))
            {
                inLangs = false;
                continue;
            }
            if (!inLangs) continue;
            if (line.Length > 3)
                blocks.Add(new Dictionary<string, string> { ["description"] = line });
        }
        return blocks;
    }

    private static void AddSection(List<ResumeSection> sections, Guid userId, Guid versionId, string type, string title, int order, object content) =>
        sections.Add(new ResumeSection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ResumeVersionId = versionId,
            SectionType = type,
            Title = title,
            SortOrder = order,
            ContentJson = JsonSerializer.Serialize(content, JsonOptions)
        });

    private static string BuildPlainResume(ResumeProfileImportData data, string rawText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.FullName)) parts.Add(data.FullName);
        parts.Add(string.Join(" | ", new[] { data.Email, data.Phone, data.City, data.Country, data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl }.Where(x => !string.IsNullOrWhiteSpace(x))));
        if (!string.IsNullOrWhiteSpace(data.TargetRole)) parts.Add($"\nOBJETIVO\n{data.TargetRole}");
        if (!string.IsNullOrWhiteSpace(data.ProfessionalSummary)) parts.Add($"\nRESUMO\n{data.ProfessionalSummary}");
        if (data.Skills.Length > 0) parts.Add($"\nCOMPETENCIAS\n{string.Join(", ", data.Skills)}");
        if (!string.IsNullOrWhiteSpace(rawText)) parts.Add($"\nTEXTO IMPORTADO\n{rawText}");
        return string.Join("\n", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private static void ApplyImport(CandidateProfile profile, ResumeProfileImportData data, string resumePath)
    {
        profile.FullName = Prefer(profile.FullName, data.FullName);
        profile.Email = Prefer(profile.Email, data.Email);
        profile.Phone = Prefer(profile.Phone, data.Phone);
        profile.City = Prefer(profile.City, data.City);
        profile.Country = Prefer(profile.Country, data.Country, "Brazil");
        profile.LinkedinUrl = Prefer(profile.LinkedinUrl, data.LinkedinUrl);
        profile.GithubUrl = Prefer(profile.GithubUrl, data.GithubUrl);
        profile.PortfolioUrl = Prefer(profile.PortfolioUrl, data.PortfolioUrl);
        profile.TargetRole = Prefer(profile.TargetRole, data.TargetRole, "ASP.NET Developer");
        profile.Seniority = Prefer(profile.Seniority, data.Seniority, "junior").ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(data.ProfessionalSummary) &&
            (string.IsNullOrWhiteSpace(profile.ProfessionalSummary) || data.ProfessionalSummary.Length > profile.ProfessionalSummary.Length / 2))
            profile.ProfessionalSummary = data.ProfessionalSummary.Trim();
        profile.Skills = MergeSkills(profile.Skills, data.Skills);
        if (!string.IsNullOrWhiteSpace(resumePath)) profile.ResumePath = resumePath;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<int> UpsertAnswersAsync(Guid userId, IEnumerable<ResumeScreeningAnswer> answers)
    {
        var count = 0;
        foreach (var answer in answers.Where(a => !string.IsNullOrWhiteSpace(a.QuestionText) && !string.IsNullOrWhiteSpace(a.AnswerValue)))
        {
            var key = string.IsNullOrWhiteSpace(answer.QuestionKey)
                ? ReusableAnswerService.StableQuestionKey(answer.QuestionText)
                : ReusableAnswerService.StableQuestionKey(answer.QuestionKey);
            var questionText = answer.QuestionText.Trim();
            var existing = await _db.ReusableAnswers.FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                (x.QuestionKey == key || x.QuestionText == questionText));
            if (existing is null)
            {
                _db.ReusableAnswers.Add(new ReusableAnswer
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    QuestionKey = key,
                    QuestionText = questionText,
                    AnswerValue = answer.AnswerValue.Trim(),
                    Category = string.IsNullOrWhiteSpace(answer.Category) ? "resume_import" : answer.Category.Trim()
                });
            }
            else
            {
                if (IsAffirmative(existing.AnswerValue) && IsNegative(answer.AnswerValue))
                {
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    count++;
                    continue;
                }
                existing.QuestionText = questionText;
                existing.AnswerValue = answer.AnswerValue.Trim();
                existing.Category = string.IsNullOrWhiteSpace(answer.Category) ? existing.Category : answer.Category.Trim();
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            count++;
        }
        return count;
    }

    private static ResumeProfileImportData BuildDeterministicImport(string resumeText, string publicText, CandidateProfile profile)
    {
        var corpus = $"{resumeText}\n{publicText}";
        var normalized = RemoveDiacritics(corpus).ToLowerInvariant();
        var skills = KnownSkills()
            .Where(skill => normalized.Contains(RemoveDiacritics(skill).ToLowerInvariant()))
            .Concat(profile.Skills)
            .Concat(InferRelatedSkills(normalized))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var email = Regex.Match(corpus, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase).Value;
        var phone = Regex.Match(corpus, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}").Value;
        var links = ExtractLinks(corpus);
        var linkedin = links.FirstOrDefault(x => x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var github = links.FirstOrDefault(x => x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";
        var portfolio = links.FirstOrDefault(x => !x.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) && !x.Contains("github.com", StringComparison.OrdinalIgnoreCase)) ?? "";

        var answers = BuildTechnicalAnswers(skills, normalized);
        var summary = string.IsNullOrWhiteSpace(profile.ProfessionalSummary)
            ? BuildSummary(corpus, skills)
            : profile.ProfessionalSummary;

        return new ResumeProfileImportData(
            profile.FullName,
            email,
            phone,
            GuessCity(corpus, profile.City),
            "Brazil",
            linkedin,
            github,
            portfolio,
            summary,
            skills,
            string.IsNullOrWhiteSpace(profile.TargetRole) ? "Desenvolvedor de Software" : profile.TargetRole,
            string.IsNullOrWhiteSpace(profile.Seniority) ? "junior" : profile.Seniority,
            answers);
    }

    private static ResumeScreeningAnswer[] BuildTechnicalAnswers(string[] skills, string normalizedCorpus)
    {
        var skillSet = new HashSet<string>(skills, StringComparer.OrdinalIgnoreCase);
        var answers = new List<ResumeScreeningAnswer>();
        AddYesNo(answers, "typescript", "Possui conhecimento/experiência com TypeScript?", HasAny(skillSet, "TypeScript"));
        AddYesNo(answers, "javascript", "Possui conhecimento/experiência com JavaScript?", HasAny(skillSet, "JavaScript", "TypeScript", "React"));
        AddYesNo(answers, "java", "Possui conhecimento sólido da linguagem Java (17+)?", HasAny(skillSet, "Java"));
        AddYesNo(answers, "spring_boot", "Possui conhecimento sólido da base do ecossistema Spring Boot?", HasAny(skillSet, "Spring Boot", "Spring"));
        AddYesNo(answers, "apis_rest", "Possui conhecimento sólido no que se refere a construção de APIs REST?", HasAny(skillSet, "API REST", "APIs REST", "REST", "ASP.NET Core"));
        AddYesNo(answers, "html_css", "Possui conhecimento sólido de HTML5/ CSS3?", HasAny(skillSet, "HTML", "HTML5", "CSS", "CSS3", "React"));
        AddYesNo(answers, "oop", "Possui experiência anterior com lógica de programação e orientação a objetos bem consolidadas?", HasAny(skillSet, "C#", ".NET", "Java", "ASP.NET Core") || normalizedCorpus.Contains("orientacao a objetos"));
        return answers.ToArray();
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

                foreach (var discovered in ExtractLinks(body).Where(IsFetchablePublicLink).Where(IsUsefulProfileLink))
                {
                    if (!seen.Contains(discovered) && !queue.Contains(discovered, StringComparer.OrdinalIgnoreCase))
                        queue.Enqueue(discovered);
                }
            }
            catch
            {
                warnings.Add($"Nao foi possivel ler link publico: {link}");
            }
        }
        return result;
    }

    private static ResumeProfileImportData MergeImportData(ResumeProfileImportData deterministic, ResumeProfileImportData ai)
    {
        return deterministic with
        {
            FullName = Prefer(deterministic.FullName, ai.FullName),
            Email = Prefer(deterministic.Email, ai.Email),
            Phone = Prefer(deterministic.Phone, ai.Phone),
            City = Prefer(deterministic.City, ai.City),
            Country = Prefer(deterministic.Country, ai.Country, "Brazil"),
            LinkedinUrl = Prefer(deterministic.LinkedinUrl, ai.LinkedinUrl),
            GithubUrl = Prefer(deterministic.GithubUrl, ai.GithubUrl),
            PortfolioUrl = Prefer(deterministic.PortfolioUrl, ai.PortfolioUrl),
            ProfessionalSummary = Prefer(ai.ProfessionalSummary, deterministic.ProfessionalSummary),
            Skills = MergeSkills(deterministic.Skills, ai.Skills),
            TargetRole = Prefer(ai.TargetRole, deterministic.TargetRole),
            Seniority = Prefer(ai.Seniority, deterministic.Seniority, "junior"),
            Answers = deterministic.Answers.Concat(ai.Answers).GroupBy(a => ReusableAnswerService.StableQuestionKey(a.QuestionKey + " " + a.QuestionText)).Select(g => g.Last()).ToArray()
        };
    }

    private static string[] KnownSkills() => [
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "SQL", "PostgreSQL", "MySQL",
        "JavaScript", "TypeScript", "React", "HTML", "HTML5", "CSS", "CSS3",
        "Java", "Spring", "Spring Boot", "Node.js", "API REST", "APIs REST",
        "Git", "GitHub", "Docker", "Playwright", "Selenium", "PHP"
    ];

    private static IEnumerable<string> InferRelatedSkills(string normalized)
    {
        if (normalized.Contains("react")) { yield return "JavaScript"; yield return "HTML5"; yield return "CSS3"; }
        if (normalized.Contains("spring boot")) yield return "Java";
        if (normalized.Contains("asp.net")) { yield return ".NET"; yield return "C#"; yield return "API REST"; }
        if (normalized.Contains("api")) yield return "API REST";
        if (normalized.Contains("typescript")) yield return "JavaScript";
        if (normalized.Contains("javascript")) { yield return "HTML5"; yield return "CSS3"; }
        if (normalized.Contains("java") && normalized.Contains("spring")) yield return "Spring Boot";
    }

    private static string[] MergeSkills(IEnumerable<string> current, IEnumerable<string> imported) =>
        current.Concat(imported)
            .Select(NormalizeSkill)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeSkill(string skill) => skill.Trim() switch
    {
        "Apis REST" => "APIs REST",
        "Api REST" => "API REST",
        "Javascript" => "JavaScript",
        "Typescript" => "TypeScript",
        var value => value
    };

    private static string Prefer(string current, string imported, string fallback = "") =>
        !string.IsNullOrWhiteSpace(current) ? current.Trim() : (!string.IsNullOrWhiteSpace(imported) ? imported.Trim() : fallback);

    private static bool HasAny(HashSet<string> skills, params string[] names) => names.Any(skills.Contains);

    private static bool IsAffirmative(string value) =>
        value.Trim().Equals("sim", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    private static bool IsNegative(string value) =>
        value.Trim().Equals("nao", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("não", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);

    private static void AddYesNo(List<ResumeScreeningAnswer> answers, string key, string question, bool value) =>
        answers.Add(new ResumeScreeningAnswer(key, question, value ? "Sim" : "Não", "resume_import"));

    private static string BuildSummary(string corpus, string[] skills)
    {
        var first = Regex.Split(corpus, @"(?<=[.!?])\s+").FirstOrDefault(s => s.Length > 80 && s.Length < 600) ?? "";
        return string.IsNullOrWhiteSpace(first)
            ? $"Desenvolvedor de Software com experiencia em {string.Join(", ", skills.Take(10))}."
            : first.Trim();
    }

    private static string GuessCity(string text, string current)
    {
        if (!string.IsNullOrWhiteSpace(current)) return current;
        var match = Regex.Match(text, @"\b([A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}]+)\s*/\s*(SP|RJ|MG|PR|SC|RS|BA|PE|CE|GO|DF)\b");
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : "";
    }

    private static string[] ExtractLinks(string text) =>
        Regex.Matches(text ?? "", @"https?://[^\s\)\]\}<>""']+", RegexOptions.IgnoreCase)
            .Select(m => NormalizeLink(m.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeLink(string value)
    {
        var link = value.TrimEnd('.', ',', ';');
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Fragment))
            link = link[..^uri.Fragment.Length];
        return link;
    }

    private static bool IsFetchablePublicLink(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        !IPAddress.TryParse(uri.Host, out _) &&
        !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsefulProfileLink(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return segments.Length == 1;
        if (host.Contains("linkedin.com"))
            return segments.Length >= 2 && segments[0].Equals("in", StringComparison.OrdinalIgnoreCase);
        return host.Contains("nicolasportie.com") || host.Contains("portfolio");
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

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
