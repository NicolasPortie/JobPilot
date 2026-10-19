namespace JobPilot.Api.Application.Contracts;

/// <summary>Habilidade a levar para o perfil, com o contexto da vaga em que ela apareceu.</summary>
public record AddProfileSkillRequest(string Skill, string? Kind, string? Reason, string? JobTitle, string? Company);
