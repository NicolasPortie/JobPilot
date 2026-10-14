using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobPilot.Api.Services;

internal enum JobSection { Unknown, Requirements, NiceToHave, Responsibilities, Benefits, About }

/// <summary>
/// Divide o texto de uma vaga por seções (Requisitos, Diferenciais, Responsabilidades,
/// Benefícios...) para que a importância de cada tecnologia venha de onde ela aparece.
/// </summary>
internal sealed class JobPostingSections
{
    private static readonly Dictionary<string, JobSection> Headings = new(StringComparer.Ordinal)
    {
        ["requisitos"] = JobSection.Requirements,
        ["requisitos obrigatorios"] = JobSection.Requirements,
        ["requisitos e qualificacoes"] = JobSection.Requirements,
        ["qualificacoes"] = JobSection.Requirements,
        ["qualificacoes obrigatorias"] = JobSection.Requirements,
        ["pre requisitos"] = JobSection.Requirements,
        ["conhecimentos obrigatorios"] = JobSection.Requirements,
        ["o que esperamos"] = JobSection.Requirements,
        ["o que buscamos"] = JobSection.Requirements,
        ["o que voce precisa ter"] = JobSection.Requirements,
        ["voce precisa"] = JobSection.Requirements,
        ["requirements"] = JobSection.Requirements,
        ["minimum qualifications"] = JobSection.Requirements,
        ["what you need"] = JobSection.Requirements,
        ["must have"] = JobSection.Requirements,
        ["required"] = JobSection.Requirements,
        ["qualifications"] = JobSection.Requirements,
        ["diferenciais"] = JobSection.NiceToHave,
        ["desejavel"] = JobSection.NiceToHave,
        ["desejaveis"] = JobSection.NiceToHave,
        ["requisitos desejaveis"] = JobSection.NiceToHave,
        ["conhecimentos desejaveis"] = JobSection.NiceToHave,
        ["sera um diferencial"] = JobSection.NiceToHave,
        ["nice to have"] = JobSection.NiceToHave,
        ["preferred"] = JobSection.NiceToHave,
        ["preferred qualifications"] = JobSection.NiceToHave,
        ["bonus"] = JobSection.NiceToHave,
        ["plus"] = JobSection.NiceToHave,
        ["pluses"] = JobSection.NiceToHave,
        ["responsabilidades"] = JobSection.Responsibilities,
        ["atribuicoes"] = JobSection.Responsibilities,
        ["atividades"] = JobSection.Responsibilities,
        ["principais atividades"] = JobSection.Responsibilities,
        ["o que voce vai fazer"] = JobSection.Responsibilities,
        ["responsibilities"] = JobSection.Responsibilities,
        ["what you will do"] = JobSection.Responsibilities,
        ["your role"] = JobSection.Responsibilities,
        ["beneficios"] = JobSection.Benefits,
        ["nossos beneficios"] = JobSection.Benefits,
        ["o que oferecemos"] = JobSection.Benefits,
        ["oferecemos"] = JobSection.Benefits,
        ["remuneracao e beneficios"] = JobSection.Benefits,
        ["benefits"] = JobSection.Benefits,
        ["what we offer"] = JobSection.Benefits,
        ["perks"] = JobSection.Benefits,
        ["sobre a empresa"] = JobSection.About,
        ["quem somos"] = JobSection.About,
        ["sobre nos"] = JobSection.About,
        ["about us"] = JobSection.About,
        ["about the company"] = JobSection.About,
        ["a empresa"] = JobSection.About,
        ["sobre a vaga"] = JobSection.Unknown,
        ["descricao da vaga"] = JobSection.Unknown,
        ["about the role"] = JobSection.Unknown
    };

    private readonly List<(int Start, JobSection Section)> _lineStarts = [];

    public string NormalizedText { get; }
    public bool HasHeadings { get; }

    public JobPostingSections(string text)
    {
        NormalizedText = Strip(text ?? "");
        var current = JobSection.Unknown;
        var offset = 0;
        foreach (var rawLine in NormalizedText.Split('\n'))
        {
            var heading = MatchHeading(rawLine);
            if (heading is { } section)
            {
                current = section;
                HasHeadings = true;
            }
            _lineStarts.Add((offset, current));
            offset += rawLine.Length + 1;
        }
    }

    public JobSection SectionAt(int index)
    {
        var result = JobSection.Unknown;
        foreach (var (start, section) in _lineStarts)
        {
            if (start > index) break;
            result = section;
        }
        return result;
    }

    // "Requisitos:" sozinho ou "Requisitos: C#, .NET" (conteúdo na mesma linha).
    private static JobSection? MatchHeading(string line)
    {
        var trimmed = line.Trim().TrimStart('#', '*', '-', '•', '●', ' ').TrimEnd('*').Trim();
        if (trimmed.Length is < 3 or > 160) return null;

        var colon = trimmed.IndexOf(':');
        var head = colon > 0 ? trimmed[..colon] : trimmed;
        if (head.Length > 45) return null;

        var key = Regex.Replace(head.ToLowerInvariant(), @"[^a-z\s]", " ");
        key = Regex.Replace(key, @"\s+", " ").Trim();
        if (!Headings.TryGetValue(key, out var section)) return null;
        // Sem dois-pontos, só vale se a linha for curta (um título de verdade).
        if (colon < 0 && trimmed.Length > 45) return null;
        return section;
    }

    private static string Strip(string value)
    {
        var decomposed = value.Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }
}
