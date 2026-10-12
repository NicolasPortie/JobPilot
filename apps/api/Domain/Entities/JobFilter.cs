namespace JobPilot.Api.Domain;

public class JobFilter
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string[] AllowedSeniorities { get; set; } = ["junior"];
    public string[] BlockedSeniorities { get; set; } = ["senior", "sênior", "sr", "sr.", "pleno", "pl", "staff", "principal", "lead", "manager", "coordenador"];
    public string[] AllowedRoleKeywords { get; set; } = ["asp.net", ".net", "c#", "full stack", "fullstack", "qa", "tester", "quality assurance", "arquiteto asp.net", "asp.net architect"];
    public string[] BlockedRoleKeywords { get; set; } = ["suporte", "support", "infra", "sre", "devops", "security", "seguranca", "data engineer", "dados"];
    public string[] AllowedWorkplaceTypes { get; set; } = ["remote"];
    public string[] BlockedWorkplaceTypes { get; set; } = ["hybrid", "híbrido", "hibrido", "onsite", "on-site", "presencial"];
    public string[] AllowedApplyTypes { get; set; } = ["linkedin_easy_apply", "inhire"];
    public int MinScore { get; set; } = 30;
    public bool AutoIgnoreLowScore { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Apply(JobFilter input)
    {
        AllowedSeniorities = input.AllowedSeniorities;
        BlockedSeniorities = input.BlockedSeniorities;
        AllowedRoleKeywords = input.AllowedRoleKeywords ?? AllowedRoleKeywords;
        BlockedRoleKeywords = input.BlockedRoleKeywords ?? BlockedRoleKeywords;
        AllowedWorkplaceTypes = input.AllowedWorkplaceTypes;
        BlockedWorkplaceTypes = input.BlockedWorkplaceTypes;
        AllowedApplyTypes = input.AllowedApplyTypes;
        MinScore = input.MinScore;
        AutoIgnoreLowScore = input.AutoIgnoreLowScore;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
