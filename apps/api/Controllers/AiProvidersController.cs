using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia os provedores de IA configurados para o usuário.
/// </summary>
[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/ai/providers")]
public sealed class AiProvidersController(AiProviderService providers) : ControllerBase
{
    /// <summary>
    /// Lista os provedores de IA do usuário.
    /// </summary>
    /// <returns>Lista de provedores de IA.</returns>
    [HttpGet]
    public async Task<ActionResult<List<AiProviderResponse>>> List() =>
        Ok(await providers.ListAsync(User.UserId()));

    /// <summary>
    /// Cria um novo provedor de IA.
    /// </summary>
    /// <param name="request">Dados do provedor a ser criado.</param>
    /// <returns>Provedor criado com status 201.</returns>
    [HttpPost]
    public async Task<ActionResult<AiProviderResponse>> Create(AiProviderRequest request)
    {
        var result = await providers.CreateAsync(User.UserId(), request);
        return this.ToActionResult(result, $"/api/ai/providers/{result.Value?.Id}");
    }

    /// <summary>
    /// Atualiza um provedor de IA existente.
    /// </summary>
    /// <param name="id">Identificador do provedor.</param>
    /// <param name="request">Dados atualizados do provedor.</param>
    /// <returns>Provedor atualizado.</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AiProviderResponse>> Update(Guid id, AiProviderRequest request) =>
        this.ToActionResult(await providers.UpdateAsync(User.UserId(), id, request));

    /// <summary>
    /// Remove um provedor de IA.
    /// </summary>
    /// <param name="id">Identificador do provedor.</param>
    /// <returns>Provedor removido.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<AiProviderResponse>> Delete(Guid id) =>
        this.ToActionResult(await providers.DeleteAsync(User.UserId(), id));

    /// <summary>
    /// Testa a conectividade de um provedor de IA.
    /// </summary>
    /// <param name="id">Identificador do provedor.</param>
    /// <returns>Resultado do teste de conectividade.</returns>
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<AiProviderTestResponse>> Test(Guid id) =>
        this.ToActionResult(await providers.TestAsync(User.UserId(), id));
}
