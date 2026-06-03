using System.Text.RegularExpressions;

namespace JobPilot.Api.Services;

public static class AnswerHumanizer
{
    private static readonly string[] Cliches =
    [
        "sou um profissional dedicado",
        "tenho paixao por tecnologia",
        "paixao por tecnologia",
        "paixão por tecnologia",
        "estou em busca de novos desafios",
        "busco novos desafios",
        "sou proativo",
        "sou proativa",
        "sou uma pessoa proativa",
        "gosto de trabalhar em equipe",
        "sou comunicativo",
        "sou comunicativa",
        "tenho facilidade de aprender",
        "aprendo rapido",
        "sou apaixonado por",
        "sou apaixonada por",
        "sempre busco a excelencia",
        "entrego resultados",
        "sou orientado a resultados",
        "sou orientada a resultados",
        "tenho espirito empreendedor",
        "penso fora da caixa",
        "sou autodidata",
        "gosto de resolver problemas complexos",
        "minha maior qualidade e",
        "acredito que minha experiencia",
        "posso agregar valor",
        "sou um asset",
        "sou um diferencial",
        "comprometimento e minha palavra-chave",
        "trabalho bem sob pressao",
        "sou multitarefa",
        "tenho perfil hands-on",
        "me considero uma pessoa",
        "sou uma pessoa que",
        "acredito fortemente em",
        "minha motivacao e",
        "tenho como objetivo",
        "busco oportunidades que me permitam",
        "meu diferencial e",
        "sou entusiasta de",
        "sou fascinado por",
        "sou fascinada por",
        "me impulsiona",
        "me move",
        "me motiva",
    ];

    private static readonly string[] GenericAdjectives =
    [
        "excelente", "otimo", "incrivel", "fantastico", "maravilhoso", "esplendido",
        "excepcional", "extraordinario", "magnifico", "supimpa", "sensacional",
        "formidavel", "notavel", "remarcavel", " admiravel", "estupendo"
    ];

    public static string Humanize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var result = text;

        foreach (var cliche in Cliches)
        {
            result = Regex.Replace(result, Regex.Escape(cliche), "", RegexOptions.IgnoreCase);
        }

        result = Regex.Replace(result, @"^\s*[-•]\s+", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"\n\s*[-•]\s+", " ", RegexOptions.Multiline);
        result = Regex.Replace(result, @"\d+\.\s+", "", RegexOptions.Multiline);

        foreach (var adj in GenericAdjectives)
        {
            result = Regex.Replace(result, $"\\b{Regex.Escape(adj)}\\b", "bom", RegexOptions.IgnoreCase);
        }

        result = Regex.Replace(result, @"\s+", " ");
        result = Regex.Replace(result, @"\s+([\.,;])", "$1");
        result = result.Trim();

        if (result.Length > 0)
        {
            result = char.ToUpper(result[0]) + result[1..];
        }

        if (!result.EndsWith('.'))
        {
            result += ".";
        }

        return result;
    }

    public static bool LooksLikeAi(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        var hitCount = Cliches.Count(c => lower.Contains(c));
        var listCount = Regex.Matches(text, @"^\s*[-•\d]").Count;
        var genericCount = GenericAdjectives.Count(a => lower.Contains(a));
        return hitCount >= 2 || listCount >= 2 || genericCount >= 3;
    }
}
