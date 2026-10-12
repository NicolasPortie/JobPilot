using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/answers")]
public sealed class AnswersController(AnswerCatalogService answers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReusableAnswerResponse>>> List() =>
        Ok(await answers.ListAnswersAsync(User.UserId()));

    [HttpPost]
    public async Task<ActionResult<ReusableAnswerResponse>> Create(ReusableAnswerRequest request)
    {
        var result = await answers.CreateAnswerAsync(User.UserId(), request);
        return this.ToActionResult(result, $"/api/answers/{result.Value?.Id}");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReusableAnswerResponse>> Update(Guid id, ReusableAnswerRequest request) =>
        this.ToActionResult(await answers.UpdateAnswerAsync(User.UserId(), id, request));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ReusableAnswerResponse>> Delete(Guid id) =>
        this.ToActionResult(await answers.DeleteAnswerAsync(User.UserId(), id));
}
