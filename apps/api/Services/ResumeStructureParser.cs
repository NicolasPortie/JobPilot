using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobPilot.Api.Services;

/// <summary>Currículo lido do texto bruto: cabeçalho e seções já separadas em itens.</summary>
internal sealed record ParsedResume(
    string FullName,
    string Headline,
    string[] HeaderLines,
    string Summary,
    string[] Skills,
    List<Dictionary<string, string>> Experience,
    List<Dictionary<string, string>> Projects,
    List<Dictionary<string, string>> Education,
    List<Dictionary<string, string>> Certifications,
    List<Dictionary<string, string>> Languages)
{
    public bool HasStructure =>
        Experience.Count > 0 || Projects.Count > 0 || Education.Count > 0 || Certifications.Count > 0 || Languages.Count > 0;
}

/// <summary>
/// Separa o texto de um currículo (PDF/DOCX/TXT) em seções e itens. Funciona com texto
/// em linhas e, como fallback, com texto achatado em uma só linha.
/// </summary>
internal static partial class ResumeStructureParser
{
    private enum Kind { None, Summary, Skills, Experience, Projects, Education, Certifications, Languages, EducationAndCertifications }

    private static readonly Dictionary<string, Kind> HeadingAliases = new(StringComparer.Ordinal)
    {
        ["RESUMO"] = Kind.Summary,
        ["RESUMO PROFISSIONAL"] = Kind.Summary,
        ["PERFIL"] = Kind.Summary,
        ["PERFIL PROFISSIONAL"] = Kind.Summary,
        ["SOBRE MIM"] = Kind.Summary,
        ["OBJETIVO"] = Kind.Summary,
        ["OBJETIVO PROFISSIONAL"] = Kind.Summary,
        ["PROFESSIONAL SUMMARY"] = Kind.Summary,
        ["SUMMARY"] = Kind.Summary,
        ["ABOUT"] = Kind.Summary,
        ["ABOUT ME"] = Kind.Summary,
        ["SKILLS"] = Kind.Skills,
        ["SKILLS E TECNOLOGIAS"] = Kind.Skills,
        ["COMPETENCIAS"] = Kind.Skills,
        ["COMPETENCIAS TECNICAS"] = Kind.Skills,
        ["HABILIDADES"] = Kind.Skills,
        ["HABILIDADES TECNICAS"] = Kind.Skills,
        ["TECNOLOGIAS"] = Kind.Skills,
        ["STACK"] = Kind.Skills,
        ["TECHNICAL SKILLS"] = Kind.Skills,
        ["CONHECIMENTOS"] = Kind.Skills,
        ["CONHECIMENTOS TECNICOS"] = Kind.Skills,
        ["EXPERIENCIA"] = Kind.Experience,
        ["EXPERIENCIA PROFISSIONAL"] = Kind.Experience,
        ["EXPERIENCIAS PROFISSIONAIS"] = Kind.Experience,
        ["HISTORICO PROFISSIONAL"] = Kind.Experience,
        ["EXPERIENCIA DE TRABALHO"] = Kind.Experience,
        ["WORK EXPERIENCE"] = Kind.Experience,
        ["PROFESSIONAL EXPERIENCE"] = Kind.Experience,
        ["EXPERIENCE"] = Kind.Experience,
        ["PROJETOS"] = Kind.Projects,
        ["PROJETOS DE DESTAQUE"] = Kind.Projects,
        ["PROJETOS PESSOAIS"] = Kind.Projects,
        ["PROJETOS RELEVANTES"] = Kind.Projects,
        ["PROJECTS"] = Kind.Projects,
        ["FORMACAO"] = Kind.Education,
        ["FORMACAO ACADEMICA"] = Kind.Education,
        ["EDUCACAO"] = Kind.Education,
        ["ESCOLARIDADE"] = Kind.Education,
        ["EDUCATION"] = Kind.Education,
        ["FORMACAO E CERTIFICACOES"] = Kind.EducationAndCertifications,
        ["FORMACAO E CURSOS"] = Kind.EducationAndCertifications,
        ["EDUCACAO E CERTIFICACOES"] = Kind.EducationAndCertifications,
        ["CERTIFICACOES"] = Kind.Certifications,
        ["CURSOS"] = Kind.Certifications,
        ["CURSOS E CERTIFICACOES"] = Kind.Certifications,
        ["CERTIFICATIONS"] = Kind.Certifications,
        ["IDIOMAS"] = Kind.Languages,
        ["LANGUAGES"] = Kind.Languages
    };

    // Títulos acentuados em maiúsculas, para reabrir linhas de um texto achatado.
    private static readonly string[] InlineHeadings =
    [
        "RESUMO PROFISSIONAL", "PERFIL PROFISSIONAL", "OBJETIVO PROFISSIONAL", "SKILLS E TECNOLOGIAS",
        "COMPETÊNCIAS TÉCNICAS", "COMPETENCIAS TECNICAS", "HABILIDADES TÉCNICAS",
        "EXPERIÊNCIA PROFISSIONAL", "EXPERIENCIA PROFISSIONAL", "PROJETOS DE DESTAQUE",
        "FORMAÇÃO E CERTIFICAÇÕES", "FORMACAO E CERTIFICACOES", "FORMAÇÃO ACADÊMICA", "FORMACAO ACADEMICA",
        "CERTIFICAÇÕES", "CERTIFICACOES", "COMPETÊNCIAS", "COMPETENCIAS", "HABILIDADES", "EXPERIÊNCIA", "EXPERIENCIA",
        "FORMAÇÃO", "FORMACAO", "EDUCAÇÃO", "EDUCACAO", "PROJETOS", "IDIOMAS", "OBJETIVO", "RESUMO", "PERFIL", "SKILLS"
    ];

    private const string Months =
        @"(?:janeiro|fevereiro|mar[cç]o|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro|jan|fev|mar|abr|mai|jun|jul|ago|set|out|nov|dez|feb|apr|may|aug|sep|oct|dec)\.?";
    private const string DatePart = @"(?:(?:" + Months + @"|\d{1,2})\s*[/\-.]\s*|" + Months + @"\s+)?(?:19|20)\d{2}";
    private const string EndPart = @"(?:" + DatePart + @"|atual(?:mente)?|presente|hoje|current|present|now)";

    [GeneratedRegex(@"\(?\s*(?<s>" + DatePart + @")\s*(?:[-–—]|\ba\b|\bat[eé]\b|\bto\b)\s*(?<e>" + EndPart + @")\s*\)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PeriodRegex();

    [GeneratedRegex(@"^[●•▪■◦·▶►‣⁃\-–—*]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\b(desenvolvedor[a]?|developer|engenheir[oa]|engineer|analista|analyst|programador[a]?|arquitet[oa]|gerente|manager|coordenador[a]?|l[ií]der|lead|consultor[a]?|especialista|t[eé]cnic[oa]|assistente|auxiliar|estagi[aá]ri[oa]|est[aá]gio|trainee|intern|designer|suporte|support|administrador[a]?|dba|devops|sre|qa|tester|scrum master|product owner|full ?stack|back ?-?end|front ?-?end)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleWordRegex();

    public static ParsedResume Parse(string? rawText)
    {
        var lines = ToLines(rawText);
        var firstHeading = lines.FindIndex(IsHeading);
        var headerLines = (firstHeading < 0 ? lines : lines.Take(firstHeading)).ToArray();
        var (fullName, headline) = ParseHeader(headerLines);

        var sections = new List<(Kind Kind, List<string> Lines)>();
        if (firstHeading >= 0)
        {
            foreach (var line in lines.Skip(firstHeading))
            {
                if (IsHeading(line)) sections.Add((HeadingAliases[NormalizeHeading(line)], []));
                else sections[^1].Lines.Add(line);
            }
        }

        var summary = "";
        var skills = new List<string>();
        var experience = new List<Dictionary<string, string>>();
        var projects = new List<Dictionary<string, string>>();
        var education = new List<Dictionary<string, string>>();
        var certifications = new List<Dictionary<string, string>>();
        var languages = new List<Dictionary<string, string>>();

        foreach (var (kind, body) in sections)
        {
            switch (kind)
            {
                case Kind.Summary:
                    summary = string.Join(" ", body.Select(StripBullet)).Trim();
                    break;
                case Kind.Skills:
                    skills.AddRange(ParseSkills(body));
                    break;
                case Kind.Experience:
                    experience.AddRange(ParseExperience(body));
                    break;
                case Kind.Projects:
                    projects.AddRange(ParseProjects(body));
                    break;
                case Kind.Education:
                case Kind.Certifications:
                case Kind.EducationAndCertifications:
                    ParseEducationBlock(body, kind, education, certifications, languages);
                    break;
                case Kind.Languages:
                    languages.AddRange(ParseLanguages(string.Join(" | ", LogicalLines(body))));
                    break;
            }
        }

        return new ParsedResume(
            fullName, headline, headerLines, summary,
            skills.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            experience, projects, education, certifications, languages);
    }


    private static List<string> ToLines(string? rawText)
    {
        var text = (rawText ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = SplitTrim(text);
        if (lines.Count < 6 && text.Length > 400)
            lines = SplitTrim(ReopenFlattenedText(text));
        return lines;
    }

    private static List<string> SplitTrim(string text) =>
        text.Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]+", " ").Trim())
            .Where(line => line.Length > 0 && !line.Equals("PDF hyperlinks:", StringComparison.OrdinalIgnoreCase) && !BareUrlRegex().IsMatch(line))
            .ToList();

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex BareUrlRegex();

    private static string ReopenFlattenedText(string text)
    {
        var alternation = string.Join("|", InlineHeadings.Select(Regex.Escape));
        var reopened = Regex.Replace(text, $@"(?<![\p{{L}}\d])({alternation})(?![\p{{L}}\d])", "\n$1\n", RegexOptions.CultureInvariant);
        reopened = Regex.Replace(reopened, @"\s*[●•▪■◦]\s*", "\n● ");
        // Cabeçalho de experiência colado ao fim do bullet anterior: "... críticas. Empresa | Cargo (Mai/2024 - Dez/2024)".
        return Regex.Replace(
            reopened,
            @"(?<=[.!?])[ \t]+(?=[^.!?\n●]{3,120}\(?\s*" + DatePart + @"\s*(?:[-–—])\s*" + EndPart + ")",
            "\n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsBullet(string line) =>
        BulletRegex().IsMatch(line) && (line.Length > 1 && (!"-–—*".Contains(line[0]) || char.IsWhiteSpace(line[1])));

    private static string StripBullet(string line) => BulletRegex().Replace(line, "").Trim();


    private static bool IsHeading(string line)
    {
        var trimmed = line.Trim().TrimEnd(':');
        if (trimmed.Length is < 3 or > 50 || IsBullet(trimmed)) return false;
        if (trimmed.Any(ch => ch is '|' or '@' || char.IsDigit(ch))) return false;
        return HeadingAliases.ContainsKey(NormalizeHeading(trimmed));
    }

    private static string NormalizeHeading(string line)
    {
        var decomposed = line.Trim().TrimEnd(':').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetter(ch) ? char.ToUpperInvariant(ch) : ' ');
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }


    private static (string FullName, string Headline) ParseHeader(string[] header)
    {
        var name = "";
        var headline = "";
        var candidates = header.Where(line => !LooksLikeContact(line)).ToArray();
        if (candidates.Length > 0 && candidates[0].Length <= 60 && !candidates[0].Any(char.IsDigit))
        {
            name = candidates[0];
            if (name == name.ToUpperInvariant())
                name = CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(name.ToLowerInvariant());
            if (candidates.Length > 1 && candidates[1].Length <= 120) headline = candidates[1];
        }
        return (name, headline);
    }

    private static bool LooksLikeContact(string line) =>
        line.Contains('@') ||
        Regex.IsMatch(line, @"\d{4,}") ||
        Regex.IsMatch(line, @"(?i)linkedin|github|portf[oó]lio|https?://");


    /// <summary>Junta linhas quebradas pelo layout do PDF em itens lógicos (um por bullet).</summary>
    private static List<string> LogicalLines(List<string> body)
    {
        var hasBullets = body.Any(IsBullet);
        var result = new List<string>();
        foreach (var line in body)
        {
            var text = StripBullet(line);
            if (text.Length == 0) continue;
            var startsNew = result.Count == 0 || IsBullet(line) || (!hasBullets && !char.IsLower(text[0]));
            if (startsNew) result.Add(text);
            else result[^1] = $"{result[^1]} {text}";
        }
        return result;
    }


    private static IEnumerable<string> ParseSkills(List<string> body)
    {
        foreach (var logical in LogicalLines(body))
        {
            var text = Regex.Replace(logical, @"^[^:,|]{2,45}:\s*", "");
            foreach (var token in SplitOutsideParentheses(text, @"[,;|•]"))
            {
                foreach (var skill in ExpandSkillToken(token))
                    yield return skill;
            }
        }
    }

    private static IEnumerable<string> ExpandSkillToken(string token)
    {
        var skill = Regex.Replace(token, @"\s*\([^)]*\)", "").Trim().TrimEnd('.', ':').Trim();
        if (skill.Length is < 1 or > 40 || skill.Contains('@')) yield break;

        // "Git/GitHub" são duas competências; "CI/CD" e "TCP/IP" não.
        var slash = Regex.Match(skill, @"^([A-Za-z]{3,})/([A-Za-z]{3,})$");
        if (slash.Success)
        {
            yield return slash.Groups[1].Value;
            yield return slash.Groups[2].Value;
            yield break;
        }
        yield return skill;
    }

    private static IEnumerable<string> SplitOutsideParentheses(string text, string separatorPattern) =>
        Regex.Split(text, $@"{separatorPattern}(?![^()]*\))").Select(part => part.Trim()).Where(part => part.Length > 0);


    private sealed class Entry
    {
        public string Company = "", Role = "", Period = "";
        public readonly List<string> Bullets = [];
        public bool HasHeader => Company.Length > 0 || Role.Length > 0;
    }

    private static List<Dictionary<string, string>> ParseExperience(List<string> body)
    {
        var entries = new List<Entry>();
        Entry? current = null;

        foreach (var line in body)
        {
            if (IsBullet(line))
            {
                current ??= AddEntry(entries);
                current.Bullets.Add(StripBullet(line));
                continue;
            }

            var period = ExtractPeriod(line, out var before, out var after);
            if (period.Length > 0)
            {
                // Texto antes do período é o cabeçalho; texto depois (texto corrido) é o primeiro bullet.
                var header = before.Length > 0 ? before : after;
                var trailing = before.Length > 0 ? after : "";

                // Período numa linha própria completa o cabeçalho anterior; com texto, abre um novo item.
                if (header.Length == 0 && current is { Period.Length: 0, Bullets.Count: 0 })
                    current.Period = period;
                else
                {
                    current = AddEntry(entries);
                    current.Period = period;
                    ApplyHeader(current, header);
                    if (trailing.Length > 0) current.Bullets.Add(trailing);
                }
                continue;
            }

            if (current is { Bullets.Count: 0 } && (!current.HasHeader || current.Company.Length == 0 || current.Role.Length == 0) && line.Length <= 90 && !line.EndsWith('.'))
            {
                ApplyHeader(current, line);
                continue;
            }

            if (current is null || LooksLikeNewEntry(current, line))
            {
                current = AddEntry(entries);
                ApplyHeader(current, line);
                continue;
            }

            if (current.Bullets.Count > 0) current.Bullets[^1] = $"{current.Bullets[^1]} {line}";
            else current.Bullets.Add(line);
        }

        return entries
            .Where(entry => entry.HasHeader || entry.Bullets.Count > 0)
            .Select(entry => Item(
                ("company", entry.Company), ("role", entry.Role), ("period", entry.Period),
                ("description", string.Join("\n", entry.Bullets))))
            .ToList();
    }

    private static Entry AddEntry(List<Entry> entries)
    {
        var entry = new Entry();
        entries.Add(entry);
        return entry;
    }

    private static bool LooksLikeNewEntry(Entry current, string line)
    {
        if (current.Bullets.Count == 0 || line.Length > 100) return false;
        var last = current.Bullets[^1];
        var previousEnded = last.EndsWith('.') || last.EndsWith(';');
        return previousEnded && !line.EndsWith('.') && (line.Contains('|') || line.Contains(" - ") || line.Contains(" – "));
    }

    /// <summary>Remove um período ("Abr/2025 - Atual") da linha e devolve-o normalizado.</summary>
    private static string ExtractPeriod(string line, out string before, out string after)
    {
        var match = PeriodRegex().Match(line);
        if (!match.Success)
        {
            before = line;
            after = "";
            return "";
        }
        before = line[..match.Index].Trim(' ', '|', '-', '–', '—', ',', ':', '(', ')');
        after = line[(match.Index + match.Length)..].Trim(' ', '|', '-', '–', '—', ',', ':', '(', ')');
        return $"{match.Groups["s"].Value.Trim()} - {match.Groups["e"].Value.Trim()}";
    }

    /// <summary>Divide "Empresa | Cargo" ou "Cargo - Empresa" usando palavras de cargo como pista.</summary>
    private static void ApplyHeader(Entry entry, string text)
    {
        text = text.Trim(' ', '|', '-', '–', '—', ',', ':');
        if (text.Length == 0) return;

        var parts = text.Contains('|')
            ? text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : Regex.Split(text, @"\s[-–—@]\s").Select(part => part.Trim()).Where(part => part.Length > 0).ToArray();

        if (parts.Length >= 2)
        {
            var first = parts[0];
            var second = string.Join(" - ", parts.Skip(1));
            var firstIsRole = TitleWordRegex().IsMatch(first);
            var secondIsRole = TitleWordRegex().IsMatch(second);
            if (firstIsRole && !secondIsRole) (entry.Role, entry.Company) = (first, second);
            else (entry.Company, entry.Role) = (first, second);
            return;
        }

        if (TitleWordRegex().IsMatch(text) && entry.Role.Length == 0) entry.Role = text;
        else if (entry.Company.Length == 0) entry.Company = text;
        else if (entry.Role.Length == 0) entry.Role = text;
    }


    private static List<Dictionary<string, string>> ParseProjects(List<string> body)
    {
        var items = new List<(string Name, List<string> Description)>();
        for (var index = 0; index < body.Count; index++)
        {
            var line = body[index];
            var text = StripBullet(line);

            if (IsBullet(line))
            {
                var colon = text.IndexOf(':');
                if (colon is > 1 and <= 80 && !text[..colon].Contains('.'))
                    items.Add((text[..colon].Trim(), [text[(colon + 1)..].Trim()]));
                else
                    items.Add(("", [text]));
                continue;
            }

            var nextIsBullet = index + 1 < body.Count && IsBullet(body[index + 1]);
            var previousComplete = items.Count == 0 || items[^1].Description.Count == 0 || items[^1].Description[^1].TrimEnd().EndsWith('.');
            if (nextIsBullet && line.Length <= 80 && previousComplete && !char.IsLower(text[0]) && !line.EndsWith('.'))
            {
                items.Add((text, []));
                continue;
            }

            if (items.Count == 0) items.Add(("", [text]));
            else if (items[^1].Description.Count > 0) items[^1].Description[^1] += " " + text;
            else items[^1].Description.Add(text);
        }

        return items
            .Where(item => item.Name.Length > 0 || item.Description.Count > 0)
            .Select(item => Item(("name", item.Name), ("description", string.Join("\n", item.Description.Where(d => d.Length > 0)))))
            .ToList();
    }


    private static void ParseEducationBlock(
        List<string> body,
        Kind kind,
        List<Dictionary<string, string>> education,
        List<Dictionary<string, string>> certifications,
        List<Dictionary<string, string>> languages)
    {
        foreach (var logical in LogicalLines(body))
        {
            var label = Regex.Match(logical, @"^(?<label>[\p{L} ]{3,25}):\s*(?<text>.+)$");
            var labelName = label.Success ? NormalizeHeading(label.Groups["label"].Value) : "";
            var text = label.Success ? label.Groups["text"].Value : logical;

            if (labelName is "IDIOMAS" or "IDIOMA" or "LANGUAGES")
                languages.AddRange(ParseLanguages(text));
            else if (labelName.StartsWith("CERTIFICACO", StringComparison.Ordinal) || labelName.StartsWith("CURSO", StringComparison.Ordinal) || labelName == "CERTIFICATIONS" || kind == Kind.Certifications)
                certifications.AddRange(ParseCertifications(text));
            else
            {
                var item = ParseEducation(text);
                if (item is not null) education.Add(item);
            }
        }
    }

    private static Dictionary<string, string>? ParseEducation(string text)
    {
        text = text.Trim().TrimEnd('.');
        if (text.Length == 0) return null;

        var year = "";
        var yearMatch = Regex.Match(text, @"\(?\b((?:19|20)\d{2})\b\)?\s*$");
        if (yearMatch.Success)
        {
            year = yearMatch.Groups[1].Value;
            text = text[..yearMatch.Index].Trim(' ', '-', '–', '—', ',', '(');
        }

        var parts = Regex.Split(text, @"\s[-–—|]\s").Select(part => part.Trim()).Where(part => part.Length > 0).ToArray();
        if (parts.Length >= 2)
        {
            var firstIsDegree = Regex.IsMatch(parts[0], @"(?i)bachar|tecn[oó]log|gradua|p[oó]s|mba|mestr|doutor|curso|t[eé]cnic|engenharia|ci[eê]ncia|an[aá]lise|sistemas");
            var degree = firstIsDegree ? parts[0] : string.Join(" - ", parts.Skip(1));
            var institution = firstIsDegree ? string.Join(" - ", parts.Skip(1)) : parts[0];
            return Item(("institution", institution), ("degree", degree), ("year", year));
        }
        return Item(("degree", text), ("year", year));
    }

    private static IEnumerable<Dictionary<string, string>> ParseCertifications(string text)
    {
        foreach (var piece in text.Split('|', ';').Select(part => part.Trim().TrimEnd('.')).Where(part => part.Length > 0))
        {
            var match = Regex.Match(piece, @"^(?<name>.+?)\s*\((?<meta>[^)]*)\)\s*$");
            if (!match.Success)
            {
                yield return Item(("name", piece));
                continue;
            }

            var meta = match.Groups["meta"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var year = meta.FirstOrDefault(value => Regex.IsMatch(value, @"^(?:19|20)\d{2}$")) ?? "";
            var issuer = string.Join(", ", meta.Where(value => value != year));
            yield return Item(("name", match.Groups["name"].Value.Trim()), ("issuer", issuer), ("year", year));
        }
    }

    private static IEnumerable<Dictionary<string, string>> ParseLanguages(string text)
    {
        foreach (var piece in SplitOutsideParentheses(text, @"[|;,]").Select(part => part.Trim().TrimEnd('.')).Where(part => part.Length > 0))
        {
            var match = Regex.Match(piece, @"^(?<lang>[^(:\-–]+?)\s*(?:\((?<level>[^)]*)\)|[:\-–]\s*(?<level2>.+))?$");
            if (!match.Success) continue;
            var level = match.Groups["level"].Success ? match.Groups["level"].Value : match.Groups["level2"].Value;
            yield return Item(("language", match.Groups["lang"].Value.Trim()), ("level", level.Trim()));
        }
    }

    private static Dictionary<string, string> Item(params (string Key, string Value)[] fields)
    {
        var item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in fields)
            if (!string.IsNullOrWhiteSpace(value)) item[key] = value.Trim();
        return item;
    }
}
