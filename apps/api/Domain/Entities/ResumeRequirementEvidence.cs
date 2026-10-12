namespace JobPilot.Api.Domain;

public class ResumeRequirementEvidence
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeEvidenceMapRunId { get; set; }
    public Guid JobRequirementId { get; set; }
    public string RequirementLabel { get; set; } = "";
    public string RequirementCategory { get; set; } = "";
    public string RequirementImportance { get; set; } = "";
    public string MatchStatus { get; set; } = "unknown";
    public string EvidenceText { get; set; } = "";
    public string EvidenceSource { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Confidence { get; set; }
    public int SortOrder { get; set; }
    public ResumeEvidenceMapRun? Run { get; set; }
}
