using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class ResumeImportSectionBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<ResumeSection> BuildSections(Guid userId, Guid versionId, ResumeProfileImportData data, string rawText)
    {
        var sections = new List<ResumeSection>();
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new { data.FullName, data.Email, data.Phone, data.City, data.State, data.Country, data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl });
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

    public static string BuildPlainResume(ResumeProfileImportData data, string rawText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.FullName)) parts.Add(data.FullName);
        parts.Add(string.Join(" | ", new[] { data.Email, data.Phone, string.IsNullOrWhiteSpace(data.State) ? data.City : $"{data.City}/{data.State}", data.Country, data.LinkedinUrl, data.GithubUrl, data.PortfolioUrl }.Where(x => !string.IsNullOrWhiteSpace(x))));
        if (!string.IsNullOrWhiteSpace(data.TargetRole)) parts.Add($"\nOBJETIVO\n{data.TargetRole}");
        if (!string.IsNullOrWhiteSpace(data.ProfessionalSummary)) parts.Add($"\nRESUMO\n{data.ProfessionalSummary}");
        if (data.Skills.Length > 0) parts.Add($"\nCOMPETENCIAS\n{string.Join(", ", data.Skills)}");
        if (!string.IsNullOrWhiteSpace(rawText)) parts.Add($"\nTEXTO IMPORTADO\n{rawText}");
        return string.Join("\n", parts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

}

