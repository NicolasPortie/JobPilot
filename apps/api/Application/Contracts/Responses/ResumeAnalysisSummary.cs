namespace JobPilot.Api.Application.Contracts;

/// <summary>Requisito da vaga com o veredito do mapa de evidências: proven, adjacent ou missing.</summary>
public record RequirementReviewItem(Guid Id, string Label, string Category, string Importance, string Status, string EvidenceText);

public record AdherenceSummary(string Level, string BadgeLabel, string Headline, string Message);

public record ResumeInsight(string Key, string Title, string Text);

/// <summary>Leitura pronta para exibição: a tela só mostra, nunca recalcula.</summary>
public record ResumeAnalysisSummary(
    AdherenceSummary Adherence,
    RequirementReviewItem[] Requirements,
    string[] ProvenLabels,
    string[] MissingSuggestions,
    ResumeInsight[] Insights);
