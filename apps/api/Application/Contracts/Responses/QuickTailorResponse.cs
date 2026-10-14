namespace JobPilot.Api.Application.Contracts;

/// <summary>Resultado de uma vaga do fluxo rápido; falhas de uma vaga não derrubam as outras.</summary>
public record QuickTailorItem(
    string Input,
    bool Succeeded,
    string? Error,
    JobTargetDetail? Target,
    TailoredResumeResponse? Tailored);

public record QuickTailorResponse(QuickTailorItem[] Items, Guid BaseResumeVersionId);

/// <summary>Currículo adaptado já gerado para uma vaga, com o estado de candidatura manual.</summary>
public record ApplicationHistoryItem(
    Guid ResumeVersionId,
    Guid? JobTargetId,
    string Role,
    string Company,
    string Location,
    string SourceUrl,
    int AtsScore,
    bool Applied,
    DateTimeOffset CreatedAt);
