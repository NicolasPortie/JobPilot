using JobPilot.Api.Application.Contracts;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Configuração da IA usada na análise de vagas e na adaptação do currículo, editável pela interface.
/// </summary>
[ApiController]
[Authorize]
[Route("api/ai/settings")]
public sealed class AiSettingsController(AiSettingsService settings) : ControllerBase
{
    /// <summary>Retorna a configuração salva, a IA em uso e os provedores disponíveis. Nunca devolve a chave.</summary>
    [HttpGet]
    public async Task<ActionResult<AiSettingsResponse>> Get() =>
        Ok(await settings.GetAsync(User.UserId()));

    /// <summary>Salva a configuração. Chave em branco mantém a chave já salva.</summary>
    [HttpPut]
    public async Task<ActionResult<AiSettingsResponse>> Save(AiSettingsRequest request) =>
        this.ToActionResult(await settings.SaveAsync(User.UserId(), request));

    /// <summary>Remove a configuração salva; a IA volta a usar a configuração local ou o Ollama.</summary>
    [HttpDelete]
    public async Task<ActionResult<AiSettingsResponse>> Delete() =>
        this.ToActionResult(await settings.DeleteAsync(User.UserId()));

    /// <summary>Testa a conexão com o que está na tela, sem salvar.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<AiProviderTestResponse>> Test(AiSettingsRequest request) =>
        this.ToActionResult(await settings.TestAsync(User.UserId(), request));
}
