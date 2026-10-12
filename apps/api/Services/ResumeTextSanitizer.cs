using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

internal static partial class ResumeTextSanitizer
{
    public static string NormalizeImportedResumeText(string value)
    {
        var text = (value ?? "").Replace("\r", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\b(OBJETIVO|RESUMO PROFISSIONAL|PERFIL PROFISSIONAL|RESUMO|COMPETENCIAS TECNICAS|COMPETÊNCIAS TÉCNICAS|COMPETENCIAS|COMPETÊNCIAS|SKILLS|EXPERIENCIA PROFISSIONAL|EXPERIÊNCIA PROFISSIONAL|EXPERIENCIA|EXPERIÊNCIA|PROJETOS|FORMACAO E CERTIFICACOES|FORMAÇÃO E CERTIFICAÇÕES|FORMACAO|FORMAÇÃO|EDUCACAO|EDUCAÇÃO|CERTIFICACOES|CERTIFICAÇÕES|IDIOMAS|TEXTO IMPORTADO)\b", "\n$1\n");
        text = Regex.Replace(text, @"\n{2,}", "\n");
        return text.Trim();
    }

    public static string CleanSummaryText(string value, CandidateProfile? profile, JsonElement? identity)
    {
        var text = CleanItemText(value);
        if (string.IsNullOrWhiteSpace(text)) return "";

        foreach (var heading in new[] { "PERFIL PROFISSIONAL", "RESUMO PROFISSIONAL", "RESUMO" })
        {
            var index = text.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                text = text[(index + heading.Length)..].Trim();
        }

        var stop = Regex.Match(text, @"(?i)\b(COMPETENCIAS|COMPETÊNCIAS|SKILLS|PROJETOS|FORMACAO|FORMAÇÃO|EDUCACAO|EDUCAÇÃO|CERTIFICACOES|CERTIFICAÇÕES|IDIOMAS|TEXTO IMPORTADO)\b");
        if (stop.Success)
            text = text[..stop.Index].Trim();

        foreach (var valueToRemove in new[]
        {
            profile?.FullName,
            profile?.Email,
            profile?.Phone,
            profile?.City,
            profile?.State,
            profile?.Country,
            JsonValue(identity, "fullName"),
            JsonValue(identity, "email"),
            JsonValue(identity, "phone"),
            JsonValue(identity, "city"),
            JsonValue(identity, "state"),
            JsonValue(identity, "country")
        })
        {
            if (!string.IsNullOrWhiteSpace(valueToRemove))
                text = text.Replace(valueToRemove, " ", StringComparison.OrdinalIgnoreCase);
        }

        text = Regex.Replace(text, @"\S+@\S+", " ");
        text = Regex.Replace(text, @"\b(?:LinkedIn|GitHub|Portfolio|Portifolio)\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?:\+?\d[\d\s().-]{7,}\d)", " ");
        text = Regex.Replace(text, @"\s*\|\s*", " ");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '|', '-', ',', '.');
        return text;
    }

    public static string CleanItemText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Replace("\r", "\n").Replace("â€¢", "\n- ").Replace("•", "\n- ").Replace("●", "\n- ").Replace("▪", "\n- ").Replace("–", "-").Replace("—", "-");
        text = Regex.Replace(text, @"(?<=[.!?])\s+([A-ZÁÉÍÓÚÂÊÔÃÕÇ][\p{L}\d\s&./+-]{2,80}\s-\s[^\n|]{2,90}\|\s(?:Jan|Fev|Mar|Abr|Mai|Jun|Jul|Ago|Set|Out|Nov|Dez)/\d{4})", "\n$1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    public static string CleanProfessionalTitle(string value)
    {
        var text = CleanItemText(value);
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = Regex.Replace(text, @"\s+", " ").Trim();

        text = Regex.Replace(text, @"(?i)^\s*(?:t[ií]tulo|cargo)\s*:\s*", " ");
        text = Regex.Replace(text, @"(?i)\bdesenvolvedor\s*\(\s*a\s*\)", "Desenvolvedor");
        text = Regex.Replace(text, @"(?i)\bprogramador\s*\(\s*a\s*\)", "Programador");
        text = Regex.Replace(text, @"(?i)\bengenheiro\s*\(\s*a\s*\)", "Engenheiro");
        text = Regex.Replace(text, @"(?i)\banalista\s*\(\s*a\s*\)", "Analista");
        text = Regex.Replace(text, @"(?i)\(\s*[a-z]\s*\)", " ");

        // Job sources often put the company or a call-to-action before the real role:
        // "Acme busca Desenvolvedor .NET". A title should describe the role only.
        var roleStart = Regex.Match(text, @"(?i)\b(?:desenvolvedor(?:a)?|programador(?:a)?|engenheiro(?:a)?|analista|arquiteto(?:a)?|especialista|consultor(?:a)?|tech\s+lead|product\s+owner|scrum\s+master|data\s+engineer|data\s+scientist|machine\s+learning|software\s+engineer|devops|sre|tester|qa)\b");
        if (roleStart.Success && roleStart.Index > 0)
            text = text[roleStart.Index..];

        // A title ends when the source starts describing the work model or the
        // requirements. Keep an in-title technology suffix such as ".NET" intact.
        text = Regex.Replace(text, @"(?i)\b(home\s*office|remot[ao]?|remote|work\s+from\s+home|h[ií]brid[oa]|hybrid|presencial|onsite|on-site)\b.*$", " ");
        text = Regex.Replace(text, @"(?i)\b(home\s*office|remot[ao]?|remote|work\s+from\s+home|h[ií]brid[oa]|hybrid|presencial|onsite|on-site)\b", " ");
        var beforeSeniority = text;
        var seniority = Regex.Match(text, @"(?i)\b(est[aá]gio|estagi[aá]ri[oa]|trainee|intern|internship|j[uú]nior|jr\.?|pleno|pl|s[eê]nior|sr\.?|mid(?:[- ]senior)?|middle)\b");
        if (seniority.Success)
        {
            var suffix = text[(seniority.Index + seniority.Length)..];
            if (string.IsNullOrWhiteSpace(suffix) || Regex.IsMatch(suffix, @"^[\s|,./()\-]*$"))
                text = text[..seniority.Index];
            else
                text = text.Remove(seniority.Index, seniority.Length);
        }
        text = Regex.Replace(text, @"(?i)\s+\b(?:com|atuando|para\s+atuar|respons[aá]vel\s+por|responsabilidades?)\b.*$", " ");
        text = Regex.Replace(text, @"(?i)\b(vaga|oportunidade|contrata(?:-se)?|busca(?:mos)?|para)\b[:\s-]*", " ");
        text = Regex.Replace(text, @"\s*[-|,/]\s*(?=$)", "");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '-', '|', ',', '/', ':');
        if (string.IsNullOrWhiteSpace(text)) return beforeSeniority.Trim();

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["desenvolvedor"] = "Desenvolvedor",
            ["desenvolvedora"] = "Desenvolvedora",
            ["programador"] = "Programador",
            ["programadora"] = "Programadora",
            ["analista"] = "Analista",
            ["engenheiro"] = "Engenheiro",
            ["engenheira"] = "Engenheira",
            ["software"] = "Software",
            ["backend"] = "Backend",
            ["back-end"] = "Back-end",
            ["frontend"] = "Frontend",
            ["front-end"] = "Front-end",
            ["fullstack"] = "Full Stack",
            ["full-stack"] = "Full Stack",
            ["dados"] = "Dados"
        };
        foreach (var pair in replacements)
            text = Regex.Replace(text, $@"(?<![\p{{L}}\d/._-]){Regex.Escape(pair.Key)}(?![\p{{L}}\d/._-])", pair.Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        text = Regex.Replace(text, @"(?i)\basp\.?\s*net(?:\s*core)?\b", match => match.Value.Contains("core", StringComparison.OrdinalIgnoreCase) ? "ASP.NET Core" : "ASP.NET");
        text = Regex.Replace(text, @"(?i)(?<![\p{L}\d])\.?\s*net(?![\p{L}\d])", ".NET");
        text = Regex.Replace(text, @"(?i)\bc\s*#\b", "C#");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '-', '|', ',', '/', ':');
        return text;
    }

    public static string CleanResumeHeadlineTitle(string value)
    {
        var original = CleanItemText(value);
        var text = CleanProfessionalTitle(original);
        if (string.IsNullOrWhiteSpace(text)) return "";

        var hadBackendSignal = Regex.IsMatch(text, @"(?i)\b(back\s*-?\s*end|backend|api(?:s)?|rest|c\s*#|asp\.?\s*net|\.?\s*net)\b");
        text = Regex.Replace(text, @"(?i)\bdev\b", "Desenvolvedor");
        text = Regex.Replace(text, @"(?i)\bdesenvolvedor\s*\(\s*a\s*\)", "Desenvolvedor");
        text = Regex.Replace(text, @"(?i)\b(est[aá]gio|estagi[aá]ri[oa]|trainee|intern|internship|j[uú]nior|jr\.?|pleno|pl|s[eê]nior|sr\.?|mid|middle)\b", " ");

        var techPattern = @"(?ix)
            (?<![\p{L}\d])
            (
                asp\.?\s*net(?:\s*core)? |
                \.?\s*net(?:\s*(?:core|[0-9]+))? |
                c\s*\# |
                angular |
                react |
                vue(?:\.js)? |
                node(?:\.js)? |
                typescript |
                javascript |
                java |
                spring(?:\s*boot)? |
                php |
                laravel |
                python |
                django |
                fastapi |
                sql(?:\s*server)? |
                postgre(?:s|sql) |
                mysql |
                mongodb
            )
            (?![\p{L}\d])
        ";
        text = Regex.Replace(text, techPattern, " ", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"\s*(?:\+|\||,|/)\s*", " ");
        text = Regex.Replace(text, @"\s*-\s*(?=$)", "");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '-', '|', ',', '/', ':', '+');

        if (hadBackendSignal && Regex.IsMatch(text, @"(?i)^(Desenvolvedor|Desenvolvedora|Programador|Programadora)$"))
            text = $"{text} Back-end";

        return string.IsNullOrWhiteSpace(text)
            ? CleanProfessionalTitle(original)
            : text;
    }

    public static string ApplyPortugueseAccents(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? "";
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["aderencia"] = "aderência",
            ["adocao"] = "adoção",
            ["ageis"] = "ágeis",
            ["analise"] = "análise",
            ["aplicacao"] = "aplicação",
            ["aplicacoes"] = "aplicações",
            ["atuacao"] = "atuação",
            ["automatico"] = "automático",
            ["automaticos"] = "automáticos",
            ["certificacao"] = "certificação",
            ["certificacoes"] = "certificações",
            ["codigo"] = "código",
            ["competencia"] = "competência",
            ["competencias"] = "competências",
            ["confianca"] = "confiança",
            ["contratacao"] = "contratação",
            ["documentacao"] = "documentação",
            ["documentacoes"] = "documentações",
            ["educacao"] = "educação",
            ["evidencia"] = "evidência",
            ["evidencias"] = "evidências",
            ["evolucao"] = "evolução",
            ["experiencia"] = "experiência",
            ["experiencias"] = "experiências",
            ["formacao"] = "formação",
            ["geracao"] = "geração",
            ["gestao"] = "gestão",
            ["historico"] = "histórico",
            ["informacao"] = "informação",
            ["informacoes"] = "informações",
            ["integracao"] = "integração",
            ["integracoes"] = "integrações",
            ["manutencao"] = "manutenção",
            ["metodologias"] = "metodologias",
            ["negocio"] = "negócio",
            ["otimizacao"] = "otimização",
            ["operacao"] = "operação",
            ["posicao"] = "posição",
            ["projecao"] = "projeção",
            ["secao"] = "seção",
            ["secoes"] = "seções",
            ["seguranca"] = "segurança",
            ["solucao"] = "solução",
            ["sustentacao"] = "sustentação",
            ["tecnica"] = "técnica",
            ["tecnicas"] = "técnicas",
            ["tecnico"] = "técnico",
            ["tecnicos"] = "técnicos",
            ["usuario"] = "usuário",
            ["validacao"] = "validação",
            ["veiculos"] = "veículos"
        };

        var text = value;
        foreach (var pair in replacements)
        {
            text = Regex.Replace(text, $@"(?<![\p{{L}}\d/._-]){Regex.Escape(pair.Key)}(?![\p{{L}}\d/._-])",
                match => MatchCasing(match.Value, pair.Value),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return text;
    }

    private static string MatchCasing(string source, string replacement)
    {
        if (source.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)))
            return replacement.ToUpperInvariant();
        if (char.IsUpper(source[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        return replacement;
    }

    public static string CleanSkill(string value)
    {
        var text = CleanItemText(value);
        text = Regex.Replace(text, @"(?i)\b(COMPETENCIAS|COMPETÊNCIAS|SKILLS|LINGUAGENS|FRAMEWORKS|BANCO DE DADOS|ARQUITETURA|FERRAMENTAS)\b[:\s-]*", "");
        text = Regex.Replace(text, @"\s{2,}", " ").Trim(' ', '|', '-', ',', ':');
        return text;
    }

    public static bool IsValidSkill(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > 55) return false;
        if (value.Contains('@')) return false;
        if (Regex.IsMatch(value, @"\d{4,}")) return false;
        if (Regex.IsMatch(value, @"(?i)\b(Nicolas|contato|telefone|penapolis|brazil|linkedin|github|portfolio|perfil profissional|objetivo|resumo)\b")) return false;
        return true;
    }

}
