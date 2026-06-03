using JobPilot.Api.Domain;

namespace JobPilot.Api.Services;

public static class ResumeEvidenceScoreCalculator
{
    public const string AlgorithmVersion = "evidence-weighted-v1";

    public static ResumeEvidenceScoreResult Calculate(IEnumerable<ResumeRequirementEvidence> evidences)
    {
        var items = evidences.ToList();
        var warnings = BuildWarnings(items);
        return new ResumeEvidenceScoreResult(
            Score(items),
            Confidence(items),
            warnings.Length > 0 ? "needs_review" : "ready",
            warnings,
            AlgorithmVersion);
    }

    private static string[] BuildWarnings(List<ResumeRequirementEvidence> items)
    {
        var warnings = new List<string>();
        var missingMustHave = items.Count(x => x.MatchStatus == "missing" && x.RequirementImportance == "must_have");
        var unknown = items.Count(x => x.MatchStatus == "unknown");
        var adjacentMustHave = items.Count(x => x.MatchStatus == "adjacent" && x.RequirementImportance == "must_have");
        if (missingMustHave > 0) warnings.Add($"{missingMustHave} requisito(s) obrigatorio(s) sem evidencia no curriculo.");
        if (adjacentMustHave > 0) warnings.Add($"{adjacentMustHave} requisito(s) obrigatorio(s) tem apenas evidencia adjacente.");
        if (unknown > 0) warnings.Add($"{unknown} requisito(s) precisam de confirmacao do usuario.");
        return warnings.ToArray();
    }

    private static int Score(List<ResumeRequirementEvidence> items)
    {
        if (items.Count == 0) return 0;
        var total = items.Sum(item => item.MatchStatus switch
        {
            "proven" => item.RequirementImportance == "must_have" ? 120 : 100,
            "adjacent" => item.RequirementImportance == "must_have" ? 65 : 70,
            "unknown" => 30,
            _ => 0
        });
        var max = items.Sum(item => item.RequirementImportance == "must_have" ? 120 : 100);
        return max == 0 ? 0 : Math.Clamp((int)Math.Round(total * 100m / max), 0, 100);
    }

    private static decimal Confidence(List<ResumeRequirementEvidence> items) =>
        items.Count == 0 ? 0m : Math.Clamp(items.Average(x => x.Confidence), 0m, 1m);
}

public sealed record ResumeEvidenceScoreResult(int Score, decimal Confidence, string Status, string[] Warnings, string AlgorithmVersion);
