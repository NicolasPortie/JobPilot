namespace JobPilot.Api.Application.Contracts;

/// <summary>
/// Pedido do fluxo rápido. <paramref name="Input"/> é um link, vários links (um por linha) ou o texto
/// da vaga; <paramref name="JobUrl"/> guarda o link quando <paramref name="Input"/> é texto colado.
/// Sem <paramref name="BaseResumeVersionId"/>, usa o último currículo base importado.
/// </summary>
public record QuickTailorRequest(
    string Input,
    string? JobUrl = null,
    bool UseAi = true,
    string? Layout = null,
    Guid? BaseResumeVersionId = null);

public record SetApplicationStatusRequest(bool Applied);
