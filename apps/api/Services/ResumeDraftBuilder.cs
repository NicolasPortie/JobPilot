using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static class ResumeDraftBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static ResumeScoreComponent Component(string key, string label, int score, int maxScore, string reason) =>
        new(key, label, score, maxScore, ScoreStatus(score, maxScore), reason);

    private static string ScoreStatus(int score, int maxScore)
    {
        if (maxScore <= 0) return "neutral";
        var ratio = (decimal)score / maxScore;
        return ratio >= 0.75m ? "good" : ratio >= 0.45m ? "attention" : "risk";
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

    public static string BuildGenericAtsPlainText(CandidateProfile? profile, ResumeVersion baseVersion, string layout)
    {
        var sb = new StringBuilder();
        var sections = baseVersion.Sections.OrderBy(x => x.SortOrder).ToList();
        var identity = ResumeTextSanitizer.SectionObject(sections, "identity");
        var fullName = ResumeTextSanitizer.FirstNonEmpty(profile?.FullName, ResumeTextSanitizer.JsonValue(identity, "fullName"), ResumeTextSanitizer.JsonValue(identity, "FullName"), "Nome nao informado");
        var targetRole = ResumeTextSanitizer.FirstNonEmpty(profile?.TargetRole, baseVersion.TargetRole);
        var skills = ExtractSkillsFromVersion(profile, baseVersion);
        ResumeTextSanitizer.Append(sb, fullName);
        ResumeTextSanitizer.Append(sb, BuildProfessionalHeadline(targetRole, skills, baseVersion.PlainText));
        ResumeTextSanitizer.Append(sb, ResumeTextSanitizer.BuildContactLine(profile, identity, baseVersion.PlainText));
        ResumeTextSanitizer.Append(sb, ResumeTextSanitizer.JoinUniqueNonEmpty(" | ",
            profile?.LinkedinUrl, ResumeTextSanitizer.JsonValue(identity, "linkedinUrl"), ResumeTextSanitizer.JsonValue(identity, "LinkedinUrl"),
            profile?.GithubUrl, ResumeTextSanitizer.JsonValue(identity, "githubUrl"), ResumeTextSanitizer.JsonValue(identity, "GithubUrl"),
            profile?.PortfolioUrl, ResumeTextSanitizer.JsonValue(identity, "portfolioUrl"), ResumeTextSanitizer.JsonValue(identity, "PortfolioUrl")));
        ResumeTextSanitizer.Blank(sb);

        var summary = BuildProfessionalSummary(profile, baseVersion, sections, identity, skills, targetRole, layout);
        if (!string.IsNullOrWhiteSpace(summary))
            ResumeTextSanitizer.Section(sb, "RESUMO PROFISSIONAL", ResumeTextSanitizer.LimitText(summary, layout == "ats_compact" ? 520 : 850));

        if (skills.Length > 0)
            ResumeTextSanitizer.Section(sb, "COMPETENCIAS", string.Join(", ", skills.Take(layout == "ats_compact" ? 24 : 36)));

        AppendSectionItems(sb, "EXPERIENCIA PROFISSIONAL", ResumeTextSanitizer.SectionItems(sections, "experience"), layout);
        AppendSectionItems(sb, "PROJETOS", ResumeTextSanitizer.SectionItems(sections, "projects"), layout);

        AppendSectionItems(sb, "FORMACAO", ResumeTextSanitizer.SectionItems(sections, "education"), layout);
        AppendSectionItems(sb, "CERTIFICACOES", ResumeTextSanitizer.SectionItems(sections, "certifications"), layout);
        AppendSectionItems(sb, "IDIOMAS", ResumeTextSanitizer.SectionItems(sections, "languages"), layout);

        AppendFallbackSections(sb, sections, baseVersion.PlainText, layout);

        return ResumeTextSanitizer.ApplyPortugueseAccents(FitToTargetLength(sb.ToString(), layout)).Trim();
    }

    public static List<ResumeSection> BuildGenericSections(Guid userId, Guid versionId, CandidateProfile? profile, ResumeVersion baseVersion, string plainText)
    {
        var sections = new List<ResumeSection>();
        var skills = ExtractSkillsFromVersion(profile, baseVersion);
        AddSection(sections, userId, versionId, "identity", "Identidade", 10, new
        {
            fullName = profile?.FullName ?? "",
            email = profile?.Email ?? "",
            phone = profile?.Phone ?? "",
            city = profile?.City ?? "",
            state = profile?.State ?? "",
            country = profile?.Country ?? "",
            linkedinUrl = profile?.LinkedinUrl ?? "",
            githubUrl = profile?.GithubUrl ?? "",
            portfolioUrl = profile?.PortfolioUrl ?? ""
        });
        var identity = ResumeTextSanitizer.SectionObject(baseVersion.Sections, "identity");
        AddSection(sections, userId, versionId, "summary", "Resumo", 20, new { text = ResumeTextSanitizer.CleanSummaryText(ResumeTextSanitizer.FirstNonEmpty(profile?.ProfessionalSummary, ResumeTextSanitizer.SectionText(baseVersion.Sections, "summary")), profile, identity) });
        AddSection(sections, userId, versionId, "skills", "Competencias", 30, new { items = skills });
        AddSection(sections, userId, versionId, "plain_text", "Texto ATS", 90, new { text = plainText });
        return sections;
    }

    public static string[] BuildGenericWarnings(string plainText, IEnumerable<ResumeSection> sections)
    {
        var warnings = new List<string>();
        if (!HasSection(sections, plainText, "experience", "EXPERIENCIA PROFISSIONAL"))
            warnings.Add("Secao de experiencia nao foi encontrada de forma estruturada; revise o curriculo base.");
        if (!HasSection(sections, plainText, "skills", "COMPETENCIAS"))
            warnings.Add("Secao de competencias nao foi encontrada de forma estruturada; revise palavras-chave reais.");
        if (plainText.Length < 600)
            warnings.Add("Curriculo ATS geral ficou curto; revise se experiencias, projetos e formacao foram importados.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ScoreReport BuildGenericScoreReport(string plainText, IEnumerable<ResumeSection> sections)
    {
        var sectionList = sections.ToList();
        var structure = 0;
        if (sectionList.Any(x => x.SectionType == "identity")) structure += 10;
        if (HasSection(sectionList, plainText, "summary", "RESUMO PROFISSIONAL")) structure += 10;
        if (HasSection(sectionList, plainText, "skills", "COMPETENCIAS")) structure += 15;
        if (HasSection(sectionList, plainText, "experience", "EXPERIENCIA PROFISSIONAL")) structure += 20;
        if (HasSection(sectionList, plainText, "education", "FORMACAO") || HasSection(sectionList, plainText, "projects", "PROJETOS")) structure += 10;

        var parseSafety = 19;
        if (plainText.Contains('\t')) parseSafety -= 5;
        if (System.Text.RegularExpressions.Regex.IsMatch(plainText, @"[│┌┐└┘■●◆★]")) parseSafety -= 8;
        var completeness = plainText.Length >= 1200 ? 13 : plainText.Length >= 700 ? 9 : 5;
        var components = new[]
        {
            Component("structure", "Estrutura ATS", Math.Clamp(structure, 0, 60), 65, "Secoes padrao detectadas e reordenadas em fluxo linear; curriculo geral ainda precisa de revisao humana."),
            Component("parse_safety", "Seguranca de parsing", Math.Clamp(parseSafety, 0, 19), 20, "Uma coluna, texto simples, sem tabelas ou elementos graficos adicionados."),
            Component("completeness", "Completude", completeness, 15, "Volume de conteudo disponivel no curriculo base, sem validar aderencia a uma vaga especifica.")
        };
        return new ScoreReport(components.Sum(x => x.Score), components);
    }

    public static void AppendSectionItems(StringBuilder sb, string title, List<Dictionary<string, string>> items, string layout)
    {
        if (items.Count == 0) return;
        var lines = new List<string>();
        var maxItems = title.Contains("PROJETOS", StringComparison.OrdinalIgnoreCase)
            ? layout == "ats_compact" ? 2 : 4
            : layout == "ats_compact" ? 4 : 7;
        var maxLines = layout == "ats_compact" ? 5 : 8;
        foreach (var item in items.Take(maxItems))
        {
            var heading = ResumeTextSanitizer.FirstNonEmpty(
                ResumeTextSanitizer.JoinNonEmpty(" - ", ResumeTextSanitizer.FirstNonEmpty(ResumeTextSanitizer.Value(item, "role"), ResumeTextSanitizer.Value(item, "degree"), ResumeTextSanitizer.Value(item, "name"), ResumeTextSanitizer.Value(item, "language")), ResumeTextSanitizer.FirstNonEmpty(ResumeTextSanitizer.Value(item, "company"), ResumeTextSanitizer.Value(item, "institution"), ResumeTextSanitizer.Value(item, "issuer"), ResumeTextSanitizer.Value(item, "level"))),
                ResumeTextSanitizer.Value(item, "title"));
            var period = ResumeTextSanitizer.FirstNonEmpty(ResumeTextSanitizer.Value(item, "period"), ResumeTextSanitizer.Value(item, "year"));
            if (!string.IsNullOrWhiteSpace(heading))
                lines.Add(string.IsNullOrWhiteSpace(period) ? heading : $"{heading} | {period}");
            var description = ResumeTextSanitizer.CleanItemText(ResumeTextSanitizer.FirstNonEmpty(ResumeTextSanitizer.Value(item, "description"), ResumeTextSanitizer.Value(item, "summary"), ResumeTextSanitizer.Value(item, "details")));
            if (!string.IsNullOrWhiteSpace(description))
            {
                foreach (var line in description.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(maxLines))
                {
                    var cleanedLine = line.TrimStart('-', ' ').Trim();
                    if (ResumeTextSanitizer.IsMalformedResumeLine(cleanedLine)) continue;
                    lines.Add(line.StartsWith("-") ? $"- {cleanedLine}" : $"- {cleanedLine}");
                }
            }
            else
            {
                var fallback = string.Join(", ", item.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).Select(pair => ResumeTextSanitizer.CleanItemText(pair.Value)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                if (!string.IsNullOrWhiteSpace(fallback) && fallback != heading && !ResumeTextSanitizer.IsMalformedResumeLine(fallback))
                    lines.Add($"- {fallback}");
            }
        }
        ResumeTextSanitizer.Section(sb, title, string.Join("\n", lines));
    }

    public static string[] ExtractSkillsFromVersion(CandidateProfile? profile, ResumeVersion version)
    {
        var values = new List<string>();
        values.AddRange(profile?.Skills ?? []);
        values.AddRange(ResumeTextSanitizer.SectionArray(version.Sections, "skills", "items"));
        values.AddRange(ResumeTextSanitizer.SectionArray(version.Sections, "skills", "skills"));
        return ResumeTextSanitizer.NormalizeSkillList(
            values.SelectMany(x => x.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
    }

    private static string BuildProfessionalHeadline(string targetRole, string[] skills, string baseText)
    {
        var title = ResumeTextSanitizer.CleanProfessionalTitle(targetRole);
        if (!string.IsNullOrWhiteSpace(title)) return title;
        return "";
    }

    private static string BuildProfessionalSummary(CandidateProfile? profile, ResumeVersion baseVersion, List<ResumeSection> sections, JsonElement? identity, string[] skills, string targetRole, string layout)
    {
        var existing = ResumeTextSanitizer.CleanSummaryText(ResumeTextSanitizer.FirstNonEmpty(profile?.ProfessionalSummary, ResumeTextSanitizer.SectionText(sections, "summary")), profile, identity);
        var sentences = SplitSentences(existing).ToList();
        var baseText = baseVersion.PlainText ?? "";
        var role = ResumeTextSanitizer.FirstNonEmpty(targetRole, profile?.Seniority);

        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(role))
            sentences.Add($"{role} com atuação em desenvolvimento de software, sistemas corporativos, integrações e análise de regras de negócio.");

        sentences = sentences
            .Where(sentence => !ResumeTextSanitizer.LooksLikeSkillListSentence(sentence, skills))
            .ToList();
        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(role))
            sentences.Add($"{role} com atuação em desenvolvimento de software, sistemas corporativos, integrações e análise de regras de negócio.");

        var experienceSignals = BuildExperienceSignals(baseText);
        if (experienceSignals.Length > 0 && !ContainsAnySentence(sentences, experienceSignals))
            sentences.Add(BuildExperienceContextSentence(experienceSignals));

        var projectSignals = BuildProjectSignals(baseText);
        if (layout != "ats_compact" && projectSignals.Length > 0 && !ContainsAnySentence(sentences, projectSignals))
            sentences.Add("Projetos com foco em backend, dados, automação de processos e interfaces web, sempre a partir de evidências presentes no currículo base.");

        return string.Join(" ", sentences.Take(layout == "ats_compact" ? 2 : 3));
    }

    private static string BuildExperienceContextSentence(string[] signals)
    {
        var text = string.Join(", ", signals.Take(4));
        if (ResumeTextSanitizer.ContainsAny(text, ["sistemas corporativos", "ERP", "sustentacao", "sustentação"]))
            return "Experiência em sustentação e evolução de sistemas corporativos, com atenção a regras de negócio, dados e integrações.";
        if (ResumeTextSanitizer.ContainsAny(text, ["API", "integracoes", "integrações", "backend"]))
            return "Experiência em desenvolvimento backend e integrações entre sistemas, com foco em aplicações web e processos internos.";
        return "Experiência prática em desenvolvimento de software, organização de soluções e entrega de funcionalidades para uso real.";
    }

    private static string[] BuildExperienceSignals(string text)
    {
        var signals = new List<string>();
        AddSignal(signals, text, "ASP.NET", "desenvolvimento backend");
        AddSignal(signals, text, ".NET 9", "desenvolvimento backend");
        AddSignal(signals, text, "Entity Framework", "dados e persistencia");
        AddSignal(signals, text, "multi-tenant", "arquitetura multi-tenant");
        AddSignal(signals, text, "API", "APIs e integrações");
        AddSignal(signals, text, "SQL", "dados e relatorios");
        AddSignal(signals, text, "ERP", "sustentação e melhoria de ERP");
        AddSignal(signals, text, "Delphi", "manutenção evolutiva em Delphi");
        return signals.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] BuildProjectSignals(string text)
    {
        var signals = new List<string>();
        AddSignal(signals, text, "PostgreSQL", "PostgreSQL");
        AddSignal(signals, text, "Next.js", "Next.js");
        AddSignal(signals, text, "Docker", "Docker");
        AddSignal(signals, text, "Redis", "Redis");
        AddSignal(signals, text, "QuestPDF", "geracao de contratos em PDF");
        AddSignal(signals, text, "Arduino", "IoT com Arduino e RFID");
        AddSignal(signals, text, "Blazor", "Blazor Server");
        return signals.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddSignal(List<string> values, string source, string term, string label)
    {
        if (ResumeTextSanitizer.ContainsTerm(source, term))
            values.Add(label);
    }

    private static IEnumerable<string> SplitSentences(string text) =>
        Regex.Split(text ?? "", @"(?<=[.!?])\s+")
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0);

    private static bool ContainsAnySentence(IEnumerable<string> sentences, string[] values) =>
        sentences.Any(sentence => values.Any(value => sentence.Contains(value, StringComparison.OrdinalIgnoreCase)));

    public static void AppendFallbackSections(StringBuilder sb, List<ResumeSection> sections, string basePlainText, string layout)
    {
        if (string.IsNullOrWhiteSpace(basePlainText)) return;
        var max = layout == "ats_compact" ? 1100 : 1800;

        if (!sections.Any(x => x.SectionType == "experience"))
        {
            var experience = ExtractFallbackSection(basePlainText,
                ["EXPERIENCIA PROFISSIONAL", "EXPERIÊNCIA PROFISSIONAL", "EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "WORK EXPERIENCE"],
                ["PROJETOS", "PROJECTS", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max);
            ResumeTextSanitizer.Section(sb, "EXPERIENCIA PROFISSIONAL", experience);
        }

        if (!sections.Any(x => x.SectionType == "projects"))
        {
            var projects = ExtractFallbackSection(basePlainText,
                ["PROJETOS", "PROJECTS"],
                ["EXPERIENCIA", "EXPERIÊNCIA", "PROFESSIONAL EXPERIENCE", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "CERTIFICACOES", "CERTIFICAÇÕES", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max / 2);
            ResumeTextSanitizer.Section(sb, "PROJETOS", projects);
        }

        if (!sections.Any(x => x.SectionType == "education"))
        {
            var education = ExtractFallbackSection(basePlainText,
                ["FORMACAO E CERTIFICACOES", "FORMAÇÃO E CERTIFICAÇÕES", "FORMACAO", "FORMAÇÃO", "EDUCACAO", "EDUCAÇÃO", "EDUCATION"],
                ["EXPERIENCIA", "EXPERIÊNCIA", "PROJETOS", "PROJECTS", "IDIOMAS", "LANGUAGES", "COMPETENCIAS", "COMPETÊNCIAS", "SKILLS"],
                max / 2);
            ResumeTextSanitizer.Section(sb, "FORMACAO", education);
        }
    }

    private static string FitToTargetLength(string value, string layout)
    {
        var maxLength = layout == "ats_compact" ? 5200 : 7800;
        var normalized = Regex.Replace(value.Replace("\r\n", "\n").Replace('\r', '\n'), @"\n{3,}", "\n\n").Trim();
        if (normalized.Length <= maxLength) return normalized;

        var sections = Regex.Split(normalized, @"(?m)^(?=[A-ZÁÀÂÃÉÊÍÓÔÕÚÇ0-9 /&]{3,}$)")
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();
        if (sections.Count <= 1)
            return ResumeTextSanitizer.LimitText(normalized, maxLength);

        var priority = new[] { "NICOLAS", "OBJETIVO", "RESUMO", "COMPETENCIAS", "EXPERIENCIA", "PROJETOS", "FORMACAO" };
        var kept = new StringBuilder();
        foreach (var section in sections.OrderBy(section =>
        {
            var firstLine = section.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var index = Array.FindIndex(priority, item => firstLine.Contains(item, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? priority.Length : index;
        }))
        {
            if (kept.Length + section.Length + 2 > maxLength) continue;
            if (kept.Length > 0) kept.AppendLine().AppendLine();
            kept.Append(section.Trim());
        }

        var fitted = kept.Length > 0 ? kept.ToString() : normalized;
        return fitted.Length <= maxLength ? fitted : ResumeTextSanitizer.LimitText(fitted, maxLength);
    }

    public static string ExtractFallbackSection(string source, string[] startLabels, string[] stopLabels, int maxLength)
    {
        var normalized = ResumeTextSanitizer.NormalizeImportedResumeText(source);
        var start = FindLabel(normalized, startLabels, 0);
        if (start.Index < 0) return "";

        var contentStart = start.Index + start.Label.Length;
        var stop = FindLabel(normalized, stopLabels, contentStart);
        var length = stop.Index >= 0 ? stop.Index - contentStart : normalized.Length - contentStart;
        if (length <= 0) return "";

        var content = normalized.Substring(contentStart, length);
        content = Regex.Replace(content, @"(?is)\bPDF hyperlinks:\s*.*$", " ");
        content = Regex.Replace(content, @"(?i)\b(TEXTO IMPORTADO|CONTEUDO DO CURRICULO BASE|CONTEÚDO DO CURRÍCULO BASE)\b", " ");
        content = ResumeTextSanitizer.CleanItemText(content);
        content = Regex.Replace(content, @"(?im)^\s*DE DESTAQUE\s*$\n?", "");
        if (content.Length < 20) return "";
        return ResumeTextSanitizer.LimitText(content, maxLength);
    }

    private static bool HasSection(IEnumerable<ResumeSection> sections, string plainText, string type, string heading) =>
        sections.Any(x => x.SectionType.Equals(type, StringComparison.OrdinalIgnoreCase)) ||
        Regex.IsMatch(RemoveDiacritics(plainText ?? ""), $@"(?im)^\s*{Regex.Escape(RemoveDiacritics(heading))}\s*$");

    private static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    public static (int Index, string Label) FindLabel(string value, string[] labels, int startAt)
    {
        if (startAt < 0 || startAt >= value.Length) return (-1, "");
        var bestIndex = -1;
        var bestLabel = "";
        foreach (var label in labels)
        {
            var match = Regex.Match(value[startAt..], $@"(?i)(^|\n)({Regex.Escape(label)})(\s|:|\n)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            var index = startAt + match.Groups[2].Index;
            if (bestIndex >= 0 && index >= bestIndex) continue;
            bestIndex = index;
            bestLabel = match.Groups[2].Value;
        }
        return (bestIndex, bestLabel);
    }

}

internal sealed record ScoreReport(int Score, ResumeScoreComponent[] Components);


