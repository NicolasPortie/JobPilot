using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = "LegacyRuntime")]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/question-patterns")]
public sealed class QuestionPatternsController(AnswerCatalogService answers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<QuestionPatternResponse>>> List() =>
        Ok(await answers.ListPatternsAsync(User.UserId()));

    [HttpPost]
    public async Task<ActionResult<QuestionPatternResponse>> Create(QuestionPatternRequest request)
    {
        var result = await answers.CreatePatternAsync(User.UserId(), request);
        return this.ToActionResult(result, $"/api/question-patterns/{result.Value?.Id}");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuestionPatternResponse>> Update(Guid id, QuestionPatternRequest request) =>
        this.ToActionResult(await answers.UpdatePatternAsync(User.UserId(), id, request));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<QuestionPatternResponse>> Delete(Guid id) =>
        this.ToActionResult(await answers.DeletePatternAsync(User.UserId(), id));

    [HttpPost("resolve")]
    public async Task<ActionResult<ResolvedAnswer>> Resolve(AnswerDraftRequest request) =>
        Ok(await answers.ResolvePatternAsync(User.UserId(), request));
}
