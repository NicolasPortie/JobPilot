using System.Text.RegularExpressions;
using JobPilot.Agent.Domain;

namespace JobPilot.Agent.Application;

/// <summary>
/// Infere o tipo semântico de um campo de formulário combinando múltiplos sinais:
/// placeholder, pattern, inputmode, autocomplete, data-mask, label keywords.
/// </summary>
public sealed class FieldTypeClassifier
{
    private static readonly Regex CpfMaskPattern = new(@"\b\d{3}[\.\s]?\d{3}[\.\s]?\d{3}[\-\s]?\d{2}\b", RegexOptions.IgnoreCase);
    private static readonly Regex CpfOnlyDigits = new(@"^\d{11}$");
    private static readonly Regex PhoneMaskPattern = new(@"[\(\[]?\d{2}[\)\]]?\s*[\-]?\s*\d{4,5}[\-\s]?\d{4}", RegexOptions.IgnoreCase);
    private static readonly Regex DateMaskPattern = new(@"\b\d{2}[\/\.\-]\d{2}[\/\.\-]\d{4}\b", RegexOptions.IgnoreCase);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    private static readonly Regex NumericOnlyPattern = new(@"^\d+$");

    /// <summary>
    /// Infere o tipo semântico de um campo baseado em múltiplos sinais.
    /// Retorna o tipo com maior score.
    /// </summary>
    public SemanticFieldType Infer(ElementCandidate element)
    {
        var scores = new Dictionary<SemanticFieldType, int>();

        // Sinal 1: Autocomplete (mais confiável — dica do navegador)
        ScoreAutoComplete(element.AutoComplete, scores);

        // Sinal 2: Placeholder (máscaras são extremamente precisas)
        ScorePlaceholder(element.Placeholder, scores);

        // Sinal 3: Pattern (regex do campo)
        ScorePattern(element.Pattern, scores);

        // Sinal 4: InputMode (teclado que o navegador mostra)
        ScoreInputMode(element.InputMode, scores);

        // Sinal 5: Data-mask / data-inputmask
        ScoreDataMask(element.DataMask, scores);

        // Sinal 6: Label keywords
        ScoreLabel(element.Label, scores);

        // Sinal 7: Name attribute (muitas vezes mais semântico que id)
        ScoreLabel(element.Name, scores);

        // Sinal 8: Title / aria-describedby
        ScoreLabel(element.Title, scores);
        ScoreLabel(element.DescribedBy ?? "", scores);

        if (scores.Count == 0) return SemanticFieldType.Unknown;

        var winner = scores.MaxBy(kv => kv.Value);
        return winner.Value >= 30 ? winner.Key : SemanticFieldType.Unknown;
    }

    /// <summary>
    /// Retorna a confiança (0.0-1.0) baseada em quantos sinais bateram.
    /// </summary>
    public decimal Confidence(ElementCandidate element, SemanticFieldType inferredType)
    {
        var scores = new Dictionary<SemanticFieldType, int>();
        ScoreAutoComplete(element.AutoComplete, scores);
        ScorePlaceholder(element.Placeholder, scores);
        ScorePattern(element.Pattern, scores);
        ScoreInputMode(element.InputMode, scores);
        ScoreDataMask(element.DataMask, scores);
        ScoreLabel(element.Label, scores);
        ScoreLabel(element.Name, scores);

        if (!scores.TryGetValue(inferredType, out var score)) return 0m;

        // Normalizar: 150+ pontos = 1.0 confiança
        return Math.Min(1.0m, score / 150m);
    }

    private static void ScoreAutoComplete(string autoComplete, Dictionary<SemanticFieldType, int> scores)
    {
        var ac = autoComplete.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ac)) return;

        var map = new Dictionary<string, SemanticFieldType>
        {
            ["email"] = SemanticFieldType.Email,
            ["tel"] = SemanticFieldType.Phone,
            ["name"] = SemanticFieldType.FullName,
            ["given-name"] = SemanticFieldType.FirstName,
            ["family-name"] = SemanticFieldType.LastName,
            ["address-line1"] = SemanticFieldType.Address,
            ["address-level2"] = SemanticFieldType.City,
            ["country"] = SemanticFieldType.Country,
            ["postal-code"] = SemanticFieldType.ZipCode,
            ["bday"] = SemanticFieldType.BirthDate,
            ["organization"] = SemanticFieldType.Address,
            ["url"] = SemanticFieldType.PortfolioUrl,
        };

        foreach (var (key, type) in map)
        {
            if (ac.Contains(key)) scores[type] = scores.GetValueOrDefault(type) + 100;
        }
    }

    private static void ScorePlaceholder(string placeholder, Dictionary<SemanticFieldType, int> scores)
    {
        var ph = placeholder.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ph)) return;

        // CPF masks
        if (ph.Contains("000.000.000-00") || ph.Contains("999.999.999-99") ||
            ph.Contains("___.___.___-__") || ph.Contains("###.###.###-##") ||
            Regex.IsMatch(ph, @"\d{3}[\.\-]\d{3}[\.\-]\d{3}[\.\-]\d{2}"))
            scores[SemanticFieldType.Cpf] = scores.GetValueOrDefault(SemanticFieldType.Cpf) + 120;

        // Phone masks
        if (ph.Contains("(00) 00000-0000") || ph.Contains("(99) 99999-9999") ||
            ph.Contains("(__) _____-____") || ph.Contains("(##) #####-####") ||
            Regex.IsMatch(ph, @"\(\d{2}\)\s*\d{4,5}[\-\s]?\d{4}"))
            scores[SemanticFieldType.Phone] = scores.GetValueOrDefault(SemanticFieldType.Phone) + 120;

        // Date masks
        if (ph.Contains("00/00/0000") || ph.Contains("__/__/____") ||
            ph.Contains("##/##/####") || Regex.IsMatch(ph, @"\d{2}[\/\.]\d{2}[\/\.]\d{4}"))
            scores[SemanticFieldType.BirthDate] = scores.GetValueOrDefault(SemanticFieldType.BirthDate) + 100;

        // Email hint
        if (ph.Contains("exemplo@email.com") || ph.Contains("seu@email.com") ||
            ph.Contains("email@dominio.com"))
            scores[SemanticFieldType.Email] = scores.GetValueOrDefault(SemanticFieldType.Email) + 100;

        // Salary / numeric
        if (ph.Contains("0,00") || ph.Contains("0.00") || ph.Contains("0000") ||
            ph.Contains("R$") || ph.Contains("$"))
            scores[SemanticFieldType.Salary] = scores.GetValueOrDefault(SemanticFieldType.Salary) + 60;

        // Label keywords within placeholder
        ScoreLabel(ph, scores, multiplier: 0.7);
    }

    private static void ScorePattern(string pattern, Dictionary<SemanticFieldType, int> scores)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;
        var pat = pattern.ToLowerInvariant();

        // CPF pattern
        if (pat.Contains(@"[0-9]{3}") && pat.Contains(@"[0-9]{2}") &&
            (pat.Contains(@"\.") || pat.Contains(@"-")) && pat.Length < 50)
            scores[SemanticFieldType.Cpf] = scores.GetValueOrDefault(SemanticFieldType.Cpf) + 100;

        // Email pattern
        if (pat.Contains("@") && pat.Contains("."))
            scores[SemanticFieldType.Email] = scores.GetValueOrDefault(SemanticFieldType.Email) + 100;

        // Numeric pattern
        if (pat.Contains(@"[0-9]") || pat.Contains(@"\d"))
        {
            scores[SemanticFieldType.Phone] = scores.GetValueOrDefault(SemanticFieldType.Phone) + 40;
            scores[SemanticFieldType.Salary] = scores.GetValueOrDefault(SemanticFieldType.Salary) + 30;
        }
    }

    private static void ScoreInputMode(string inputMode, Dictionary<SemanticFieldType, int> scores)
    {
        var im = inputMode.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(im)) return;

        if (im == "email") scores[SemanticFieldType.Email] = scores.GetValueOrDefault(SemanticFieldType.Email) + 80;
        if (im == "tel") scores[SemanticFieldType.Phone] = scores.GetValueOrDefault(SemanticFieldType.Phone) + 80;
        if (im == "numeric") scores[SemanticFieldType.Cpf] = scores.GetValueOrDefault(SemanticFieldType.Cpf) + 50;
        if (im == "url") scores[SemanticFieldType.PortfolioUrl] = scores.GetValueOrDefault(SemanticFieldType.PortfolioUrl) + 60;
    }

    private static void ScoreDataMask(string dataMask, Dictionary<SemanticFieldType, int> scores)
    {
        var dm = dataMask.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(dm)) return;

        if (dm.Contains("000.000.000-00") || dm.Contains("999.999.999-99") ||
            Regex.IsMatch(dm, @"\d{3}[\.\-]\d{3}[\.\-]\d{3}[\.\-]\d{2}"))
            scores[SemanticFieldType.Cpf] = scores.GetValueOrDefault(SemanticFieldType.Cpf) + 120;

        if (dm.Contains("(00) 00000-0000") || dm.Contains("(99) 99999-9999") ||
            Regex.IsMatch(dm, @"\(\d{2}\)\s*\d{4,5}[\-\s]?\d{4}"))
            scores[SemanticFieldType.Phone] = scores.GetValueOrDefault(SemanticFieldType.Phone) + 120;

        if (dm.Contains("00/00/0000") || Regex.IsMatch(dm, @"\d{2}[\/\.]\d{2}[\/\.]\d{4}"))
            scores[SemanticFieldType.BirthDate] = scores.GetValueOrDefault(SemanticFieldType.BirthDate) + 100;
    }

    private static void ScoreLabel(string text, Dictionary<SemanticFieldType, int> scores, double multiplier = 1.0)
    {
        var txt = text.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(txt)) return;

        var keywords = new (string[] Words, SemanticFieldType Type, int BaseScore)[]
        {
            (["cpf", "cadastro de pessoa fisica"], SemanticFieldType.Cpf, 90),
            (["cnpj"], SemanticFieldType.Cnpj, 90),
            (["email", "e-mail", "correio eletronico"], SemanticFieldType.Email, 80),
            (["telefone", "fone", "celular", "whatsapp", "mobile", "phone"], SemanticFieldType.Phone, 80),
            (["nome completo", "full name"], SemanticFieldType.FullName, 80),
            (["primeiro nome", "first name", "given name"], SemanticFieldType.FirstName, 80),
            (["sobrenome", "last name", "family name", "surname"], SemanticFieldType.LastName, 80),
            (["nome"], SemanticFieldType.FullName, 50),
            (["cidade", "city", "location", "localizacao", "local", "current location", "where are you based", "onde voce mora", "onde você mora"], SemanticFieldType.City, 70),
            (["pais", "country"], SemanticFieldType.Country, 70),
            (["cep", "zip", "postal code"], SemanticFieldType.ZipCode, 70),
            (["endereco", "address", "rua"], SemanticFieldType.Address, 70),
            (["linkedin"], SemanticFieldType.LinkedInUrl, 80),
            (["github"], SemanticFieldType.GitHubUrl, 80),
            (["portfolio", "portifolio", "website", "site"], SemanticFieldType.PortfolioUrl, 80),
            (["pretensao", "salary", "remuneracao", "compensation", "expected salary"], SemanticFieldType.Salary, 70),
            (["quantos anos", "anos de experiencia", "years of experience", "how many years", "anos voce usa", "anos você usa", "anos ja usa", "anos já usa"], SemanticFieldType.ExperienceYears, 85),
            (["data de nascimento", "birth date", "birthday", "nascimento"], SemanticFieldType.BirthDate, 80),
            (["data de inicio", "start date", "available from", "disponivel a partir"], SemanticFieldType.StartDate, 70),
            (["periodo de aviso", "notice period", "aviso previo"], SemanticFieldType.NoticePeriod, 70),
            (["curriculo", "resume", "cv"], SemanticFieldType.ResumeUpload, 70),
            (["carta de apresentacao", "cover letter"], SemanticFieldType.CoverLetterUpload, 70),
            (["genero", "gender", "sexo"], SemanticFieldType.Gender, 60),
            (["raca", "race", "etnia", "ethnicity"], SemanticFieldType.Race, 60),
            (["deficiencia", "disability", "pcd"], SemanticFieldType.Disability, 60),
            (["veterano", "veteran"], SemanticFieldType.VeteranStatus, 60),
        };

        foreach (var (words, type, baseScore) in keywords)
        {
            if (words.Any(w => txt.Contains(w)))
            {
                scores[type] = scores.GetValueOrDefault(type) + (int)(baseScore * multiplier);
            }
        }
    }
}
