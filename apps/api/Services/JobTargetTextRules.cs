using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JobPilot.Api.Application.Contracts;

namespace JobPilot.Api.Services;

internal static class JobTargetTextRules
{
    public static readonly string[] KnownTech =
    [
        "C#", ".NET", ".NET Core", ".NET 8", ".NET 9", "ASP.NET", "ASP.NET Core", "Entity Framework", "Entity Framework Core", "SQL", "SQL Server", "PostgreSQL", "MySQL", "SQLite",
        "JavaScript", "TypeScript", "React", "React.js", "Angular", "Vue", "Vue.js", "Node.js", "Java", "Python", "Go", "Rust",
        "Spring Boot", "API REST", "APIs REST", "Web APIs REST", "REST", "RESTful", "gRPC", "GraphQL", "OpenAPI", "Swagger",
        "Docker", "Kubernetes", "Git", "GitHub", "GitLab", "Bitbucket",
        "Azure", "Azure DevOps", "AWS", "Amazon Web Services", "GCP", "Google Cloud", "Firebase",
        "RabbitMQ", "Kafka", "MongoDB", "Redis", "Elasticsearch", "Solr", "CI/CD", "DevOps", "GitOps",
        "Orientação a objetos", "OOP", "Injeção de dependência", "Dependency Injection", "IoC", "Clean Architecture", "Clean Code",
        "CQRS", "MediatR", "AutoMapper", "FluentValidation", "xUnit", "NUnit", "MSTest", "Unit Testing",
        "Express.js", "Selenium", "Playwright", "Cypress", "QA", "Testes", "TDD", "BDD",
        "HTML", "CSS", "SASS", "LESS", "Bootstrap", "Tailwind CSS", "Material UI", "Next.js", "Nuxt.js",
        "Blazor", "WPF", "WinForms", "Xamarin", "MAUI", "Flutter", "React Native",
        "Dapper", "NHibernate", "LINQ", "Async/Await", "Parallel Programming", "Multithreading",
        "OAuth", "JWT", "Identity Server", "Keycloak", "Auth0",
        "Prometheus", "Grafana", "Datadog", "New Relic", "ELK Stack", "Splunk",
        "Terraform", "Ansible", "Pulumi", "CloudFormation",
        "Jenkins", "GitHub Actions", "GitLab CI", "CircleCI", "Travis CI", "TeamCity",
        "Nginx", "Apache", "IIS", "HAProxy", "Traefik", "Envoy",
        "Linux", "Windows Server", "Ubuntu", "Debian", "CentOS", "RHEL",
        "Microservices", "Serverless", "Lambda", "Azure Functions", "Event-Driven Architecture",
        "DDD", "Domain-Driven Design", "Event Sourcing", "Saga Pattern", "Circuit Breaker",
        "OAuth2", "OpenID Connect", "SAML", "LDAP", "Active Directory",
        "Postman", "Insomnia", "Hoppscotch", "Swagger UI", "Redoc",
        "Jira", "Confluence", "Trello", "Azure Boards", "Asana",
        "Scrum", "Kanban", "Agile", "Pair Programming", "Code Review",
        "Figma", "Adobe XD", "Sketch", "InVision",
        "Power BI", "Tableau", "Looker", "Metabase", "Grafana",
        "SSIS", "SSAS", "SSRS", "ETL", "Data Warehouse", "Data Lake", "BigQuery", "Snowflake",
        "Pandas", "NumPy", "SciPy", "Scikit-learn", "TensorFlow", "PyTorch", "Keras", "OpenCV",
        "NLTK", "SpaCy", "Hugging Face", "LangChain", "OpenAI", "LLM",
        "Blockchain", "Ethereum", "Solidity", "Web3", "Smart Contracts", "Hyperledger",
        "Unity", "Unreal Engine", "Godot", "Game Development",
        "Unity 3D", "ARKit", "ARCore",
        "IoT", "MQTT", "CoAP", "LoRaWAN", "Zigbee", "Bluetooth", "BLE",
        "Raspberry Pi", "Arduino", "ESP32", "STM32", "FPGA",
        "MATLAB", "Simulink", "LabVIEW", "AutoCAD", "SolidWorks", "CATIA",
        "SAP", "Oracle", "Salesforce", "Dynamics 365", "ServiceNow", "Workday",
        "Magento", "Shopify", "WooCommerce", "PrestaShop", "BigCommerce",
        "WordPress", "Drupal", "Joomla", "Laravel", "Symfony", "Django", "Flask", "FastAPI",
        "Ruby", "Ruby on Rails", "PHP", "Perl", "Scala", "Kotlin", "Swift", "Objective-C",
        "C++", "Assembly", "Fortran", "COBOL", "Delphi", "Pascal", "VB.NET", "VBA",
        "Haskell", "Erlang", "Elixir", "Clojure", "F#", "OCaml", "Lisp", "Prolog",
        "Julia", "SAS", "SPSS", "Stata", "Minitab",
        "Shell Script", "Bash", "PowerShell",
        "Maven", "Gradle", "Ant", "MSBuild", "Cake", "Paket", "NuGet", "npm", "yarn", "pnpm",
        "Webpack", "Vite", "Rollup", "Parcel", "esbuild", "Turbopack", "Babel", "SWC",
        "ESLint", "Prettier", "Stylelint", "Husky", "lint-staged",
        "Storybook", "Chromatic", "Loki", "Percy", "BackstopJS",
        "Jest", "Vitest", "Mocha", "Chai", "Jasmine", "Karma", "Protractor", "Cucumber",
        "Puppeteer", "Nightwatch", "WebDriverIO", "Appium", "Detox",
        "SonarQube", "CodeClimate", "Snyk", "Dependabot", "WhiteSource", "Black Duck",
        "OWASP", "Penetration Testing", "Ethical Hacking", "Kali Linux", "Metasploit",
        "ISO 27001", "SOC 2", "GDPR", "LGPD", "HIPAA", "PCI DSS", "NIST", "COBIT", "ITIL"
    ];

    public static string TruncateEvidence(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return "";
        var cleaned = Regex.Replace(evidence, @"\s+", " ").Trim();
        cleaned = ExtractCleanSnippet(cleaned);
        return cleaned.Length <= 70 ? cleaned : cleaned[..67].Trim() + "...";
    }

    private static string ExtractCleanSnippet(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= 70) return text;
        if (text.StartsWith("..."))
        {
            var afterEllipsis = text[3..].TrimStart();
            var firstSpace = afterEllipsis.IndexOf(' ');
            text = firstSpace > 0 && firstSpace < 20 ? afterEllipsis[firstSpace..].TrimStart() : afterEllipsis;
        }

        var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (lines.Count > 0 && lines[0].Length <= 70) return lines[0];
        var sentenceEnd = FindSentenceEnd(text, 70);
        if (sentenceEnd > 15) return text[..(sentenceEnd + 1)].Trim();
        var breakPoint = text.LastIndexOf(' ', Math.Min(text.Length, 70));
        return breakPoint > 40 ? text[..breakPoint].Trim() : text[..70].Trim();
    }

    private static int FindSentenceEnd(string text, int maxLength)
    {
        var limit = Math.Min(text.Length, maxLength);
        var sentenceEnd = -1;
        for (var index = 0; index < limit; index++)
        {
            var ch = text[index];
            if (ch is '!' or '?' or ';' || ch == '.' && (index == text.Length - 1 || char.IsWhiteSpace(text[index + 1]))) sentenceEnd = index;
        }
        return sentenceEnd;
    }

    public static bool IsStructuredJobText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        var markers = new[] { "requisitos", "desejavel", "diferencial", "beneficios", "responsabilidades", "sobre a vaga", "atribuicoes", "experiencia", "formacao" };
        return markers.Count(marker => normalized.Contains(marker)) >= 2;
    }

    public static string ExtractTitleFromFirstLine(string? sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return "";
        var lines = sourceText.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 3 && line.Length < 120).ToList();
        var titlePatterns = new[] { "Desenvolvedor", "Engenheiro", "Analista", "Programador", "Arquiteto", "Especialista", "Consultor", "Tech Lead", "Product Owner", "Scrum Master", "QA", "Tester", "DevOps", "SRE", "Data Engineer", "Data Scientist", "Machine Learning" };
        foreach (var line in lines.Take(5))
        {
            if (!titlePatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;
            var cleaned = line.Split(['·', '|', '—', '\t']).FirstOrDefault()?.Trim() ?? line;
            if (cleaned.Length >= 5 && cleaned.Length <= 100) return cleaned;
        }
        return "";
    }

    public static string InferTitleFromRequirements(JobRequirementItem[] requirements)
    {
        var techs = requirements.Where(requirement => requirement.Category == "technology").Select(requirement => requirement.Label).ToList();
        if (techs.Contains("C#", StringComparer.OrdinalIgnoreCase) || techs.Contains(".NET", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor .NET";
        if (techs.Contains("Java", StringComparer.OrdinalIgnoreCase) || techs.Contains("Spring Boot", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor Java";
        if (techs.Contains("Python", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor Python";
        if (techs.Contains("React", StringComparer.OrdinalIgnoreCase) || techs.Contains("Angular", StringComparer.OrdinalIgnoreCase) || techs.Contains("Vue", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor Frontend";
        if (techs.Contains("JavaScript", StringComparer.OrdinalIgnoreCase) || techs.Contains("TypeScript", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor Fullstack";
        if (techs.Contains("Node.js", StringComparer.OrdinalIgnoreCase)) return "Desenvolvedor Backend";
        return "";
    }

    public static string EvidenceAround(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalizedText = RemoveDiacritics(text).ToLowerInvariant();
        var normalizedTerm = RemoveDiacritics(term).ToLowerInvariant();
        var index = normalizedText.IndexOf(normalizedTerm, StringComparison.Ordinal);
        if (index < 0) return text[..Math.Min(text.Length, 100)].Trim();

        var rawStart = Math.Max(0, index - 35);
        var start = rawStart;
        if (rawStart > 0)
        {
            var nextSpace = text.IndexOf(' ', rawStart);
            if (nextSpace > 0 && nextSpace < index) start = nextSpace + 1;
        }

        var end = Math.Min(text.Length, index + term.Length + 55);
        var snippet = text[start..end].Trim();
        if (start > 0) snippet = "..." + snippet;
        if (end < text.Length) snippet += "...";
        return snippet.Length > 100 ? snippet[..97].Trim() + "..." : snippet;
    }

    public static string BuildSummary(string text)
    {
        var cleaned = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return cleaned.Length <= 420 ? cleaned : cleaned[..420].Trim();
    }

    public static bool LooksMustHave(string normalizedText, string keyword)
    {
        var normalizedKeyword = RemoveDiacritics(keyword).ToLowerInvariant();
        var index = normalizedText.IndexOf(normalizedKeyword, StringComparison.Ordinal);
        if (index < 0) return false;
        var start = Math.Max(0, index - 200);
        var window = normalizedText[start..Math.Min(normalizedText.Length, index + normalizedKeyword.Length + 200)];
        return ContainsAny(window, ["obrigatorio", "obrigatoria", "necessario", "necessaria", "requisitos", "requisito", "must", "required", "experiencia com", "experiencia em", "conhecimento em", "dominio de", "habilidades", "habilidade", "competencias", "competencia"]);
    }

    public static string DetectSeniority(string normalized)
    {
        if (ContainsSeniorityTerm(normalized, ["lead", "principal", "staff", "manager", "coordenador"])) return "lead";
        if (ContainsSeniorityTerm(normalized, ["mid-senior", "mid senior", "mid-level", "mid level"])) return "mid";
        if (ContainsSeniorityTerm(normalized, ["senior", "sênior", "sr.", "sr"])) return "senior";
        if (ContainsSeniorityTerm(normalized, ["pleno", "pl", "mid-level", "mid level", "mid"])) return "mid";
        if (ContainsSeniorityTerm(normalized, ["junior", "jr", "júnior"])) return "junior";
        if (ContainsSeniorityTerm(normalized, ["estagio", "estagiario", "estagiaria", "trainee", "internship", "intern"])) return "intern";
        return "unknown";
    }

    private static bool ContainsSeniorityTerm(string normalized, IEnumerable<string> terms) => terms.Any(term =>
    {
        var normalizedTerm = RemoveDiacritics(term.ToLowerInvariant()).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;
        var pattern = $@"(^|[^a-z0-9]){Regex.Escape(normalizedTerm)}([^a-z0-9]|$)";
        return Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase);
    });

    public static string DetectWorkplace(string normalized)
    {
        if (ContainsAny(normalized, ["remoto", "remota", "remote", "home office", "work from home"])) return "remote";
        if (ContainsAny(normalized, ["hibrido", "híbrido", "hybrid"])) return "hybrid";
        if (ContainsAny(normalized, ["presencial", "onsite", "on-site"])) return "onsite";
        return "unknown";
    }

    public static bool ContainsAny(string value, IEnumerable<string> terms) => terms.Any(term => ContainsTerm(value, term));

    public static bool ContainsTerm(string normalizedHaystack, string term)
    {
        var normalizedTerm = RemoveDiacritics(term.ToLowerInvariant()).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTerm)) return false;
        if (normalizedTerm.Length <= 3 || normalizedTerm.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            var pattern = $@"(^|[^a-z0-9]){Regex.Escape(normalizedTerm)}([^a-z0-9]|$)";
            return Regex.IsMatch(normalizedHaystack, pattern, RegexOptions.IgnoreCase);
        }
        return normalizedHaystack.Contains(normalizedTerm);
    }

    public static string NormalizeKeyword(string value)
    {
        var normalized = RemoveDiacritics(value.ToLowerInvariant()).Trim();
        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "requisito" : normalized;
    }

    public static string RemoveDiacritics(string value)
    {
        var normalized = (value ?? "").Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
