namespace JobPilot.Api.Domain;

public class ResumeEvidenceMapRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid JobTargetId { get; set; }
    public string Status { get; set; } = "ready";
    public int EvidenceScore { get; set; }
    public decimal Confidence { get; set; }
    public string WarningsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ResumeRequirementEvidence> Items { get; set; } = [];
}
