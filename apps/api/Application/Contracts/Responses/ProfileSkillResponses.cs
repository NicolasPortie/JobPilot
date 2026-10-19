namespace JobPilot.Api.Application.Contracts;

public record ProfileSkillRecommendationResponse(Guid Id, string Skill, string Kind, string Reason, string JobTitle, string Company, DateTimeOffset CreatedAt);

/// <summary>Resultado de adicionar uma habilidade: o perfil atualizado e se ela já existia.</summary>
public record AddProfileSkillResponse(CandidateProfileResponse Profile, bool AlreadyPresent);
