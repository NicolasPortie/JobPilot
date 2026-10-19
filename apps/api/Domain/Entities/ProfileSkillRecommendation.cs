namespace JobPilot.Api.Domain;

/// <summary>Habilidade que o usuário decidiu levar para o perfil a partir de uma análise de vaga.</summary>
public class ProfileSkillRecommendation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Skill { get; set; } = "";
    public string Kind { get; set; } = "missing";
    public string Reason { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Company { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
