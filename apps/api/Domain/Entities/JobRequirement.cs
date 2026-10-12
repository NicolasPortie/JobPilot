namespace JobPilot.Api.Domain;

public class JobRequirement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid JobTargetId { get; set; }
    public string Category { get; set; } = "";
    public string Label { get; set; } = "";
    public string Importance { get; set; } = "context";
    public string EvidenceText { get; set; } = "";
    public string NormalizedKeyword { get; set; } = "";
    public string[] ExactPhrases { get; set; } = [];
    public int SortOrder { get; set; }
    public JobTarget? JobTarget { get; set; }
}
