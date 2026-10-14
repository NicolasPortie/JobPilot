using JobPilot.Api.Application;
using JobPilot.Api.Domain;
using JobPilot.Api.Infrastructure;
using JobPilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPilot.Api.Controllers;

/// <summary>
/// Gerencia currículos do usuário, incluindo importações, seções, personalização e exportações.
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Analysis)]
[Route("api/resume")]
public sealed class ResumeController(
    ResumeQueryService queries,
    ResumeSectionService sections,
    IResumeTailoringService tailoring,
    ResumeExportService exports,
    ResumeReviewAnswerService reviewAnswers) : ControllerBase
{
    /// <summary>
    /// Lista os currículos importados do usuário.
    /// </summary>
    /// <returns>Lista de resumos de importações.</returns>
    [HttpGet("imports")]
    public async Task<ActionResult<List<ResumeImportSummary>>> ListImports() =>
        Ok(await queries.ListImportsAsync(User.UserId()));

    /// <summary>
    /// Lista os documentos de currículo do usuário.
    /// </summary>
    /// <returns>Lista de resumos de documentos.</returns>
    [HttpGet("documents")]
    public async Task<ActionResult<List<ResumeDocumentSummary>>> ListDocuments() =>
        Ok(await queries.ListDocumentsAsync(User.UserId()));

    /// <summary>
    /// Retorna as opções de layout disponíveis para currículos.
    /// </summary>
    /// <returns>Opções de layout.</returns>
    [HttpGet("layouts")]
    public ActionResult<ResumeLayoutOption[]> ListLayouts() => Ok(ResumeTailoringService.LayoutOptions);

    /// <summary>
    /// Retorna os detalhes de uma versão de currículo.
    /// </summary>
    /// <param name="id">Identificador da versão.</param>
    /// <returns>Versão encontrada ou 404.</returns>
    [HttpGet("versions/{id:guid}")]
    public async Task<ActionResult<ResumeVersionDetail>> GetVersion(Guid id)
    {
        var version = await queries.GetVersionAsync(User.UserId(), id);
        return version is null ? NotFound() : Ok(version);
    }

    /// <summary>
    /// Lista as seções de uma versão de currículo.
    /// </summary>
    /// <param name="id">Identificador da versão.</param>
    /// <returns>Lista de seções.</returns>
    [HttpGet("versions/{id:guid}/sections")]
    public async Task<ActionResult<ResumeSectionDto[]>> ListSections(Guid id) =>
        Ok(await sections.ListAsync(User.UserId(), id));

    /// <summary>
    /// Adiciona uma nova seção a uma versão de currículo.
    /// </summary>
    /// <param name="id">Identificador da versão.</param>
    /// <param name="request">Dados da seção a ser adicionada.</param>
    /// <returns>Seção criada.</returns>
    [HttpPost("versions/{id:guid}/sections")]
    public async Task<ActionResult<ResumeSectionDto>> AddSection(Guid id, CreateResumeSectionRequest request) =>
        Ok(await sections.AddAsync(User.UserId(), id, request));

    /// <summary>
    /// Reordena as seções de uma versão de currículo.
    /// </summary>
    /// <param name="id">Identificador da versão.</param>
    /// <param name="request">Ordem desejada das seções.</param>
    /// <returns>Seções reordenadas.</returns>
    [HttpPost("versions/{id:guid}/sections/reorder")]
    public async Task<ActionResult<ResumeSectionDto[]>> ReorderSections(Guid id, ReorderResumeSectionsRequest request) =>
        Ok(await sections.ReorderAsync(User.UserId(), id, request));

    /// <summary>
    /// Atualiza uma seção de currículo.
    /// </summary>
    /// <param name="sectionId">Identificador da seção.</param>
    /// <param name="request">Dados atualizados da seção.</param>
    /// <returns>Seção atualizada.</returns>
    [HttpPut("sections/{sectionId:guid}")]
    public async Task<ActionResult<ResumeSectionDto>> UpdateSection(Guid sectionId, UpdateResumeSectionRequest request) =>
        Ok(await sections.UpdateAsync(User.UserId(), sectionId, request));

    /// <summary>
    /// Remove uma seção de currículo.
    /// </summary>
    /// <param name="sectionId">Identificador da seção.</param>
    /// <returns>Status 204.</returns>
    [HttpDelete("sections/{sectionId:guid}")]
    public async Task<IActionResult> DeleteSection(Guid sectionId)
    {
        await sections.DeleteAsync(User.UserId(), sectionId);
        return NoContent();
    }

    /// <summary>
    /// Personaliza um currículo para uma vaga específica.
    /// </summary>
    /// <param name="request">Dados da vaga e currículo para personalização.</param>
    /// <returns>Currículo personalizado.</returns>
    [HttpPost("tailor")]
    public async Task<ActionResult<TailoredResumeResponse>> Tailor(TailorResumeRequest request) =>
        Ok(await tailoring.TailorAsync(User.UserId(), request));

    /// <summary>
    /// Constrói um currículo genérico otimizado para ATS.
    /// </summary>
    /// <param name="request">Dados para construção do currículo ATS.</param>
    /// <returns>Currículo ATS gerado.</returns>
    [HttpPost("ats-build")]
    public async Task<ActionResult<TailoredResumeResponse>> BuildAts(BuildGenericAtsResumeRequest request) =>
        Ok(await tailoring.BuildGenericAtsAsync(User.UserId(), request));

    /// <summary>
    /// Exporta uma versão de currículo no formato solicitado.
    /// </summary>
    /// <param name="id">Identificador da versão.</param>
    /// <param name="request">Configuração da exportação.</param>
    /// <returns>Resultado da exportação.</returns>
    [HttpPost("versions/{id:guid}/exports")]
    public async Task<ActionResult<ResumeExportResponse>> Export(Guid id, CreateResumeExportRequest request) =>
        Ok(await exports.ExportAsync(User.UserId(), id, request));

    /// <summary>
    /// Gera a previa HTML de uma versao de curriculo usando o mesmo template da exportacao.
    /// </summary>
    /// <param name="id">Identificador da versao.</param>
    /// <param name="request">Configuracao da previa.</param>
    /// <returns>HTML usado para previa e exportacao.</returns>
    [HttpPost("versions/{id:guid}/preview")]
    public async Task<ActionResult<ResumePreviewResponse>> Preview(Guid id, CreateResumeExportRequest request) =>
        Ok(await exports.PreviewAsync(User.UserId(), id, request));

    /// <summary>
    /// Faz o download de uma exportação de currículo.
    /// </summary>
    /// <param name="id">Identificador da exportação.</param>
    /// <returns>Arquivo da exportação ou 404.</returns>
    [HttpGet("exports/{id:guid}/download")]
    public async Task<IActionResult> DownloadExport(Guid id)
    {
        var export = await exports.GetAsync(User.UserId(), id);
        if (export is null)
            return NotFound();
        if (!System.IO.File.Exists(export.FilePath))
            return NotFound();

        var contentType = export.Format.Equals("docx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : export.Format.Equals("word", StringComparison.OrdinalIgnoreCase)
                ? "application/msword"
            : export.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "application/octet-stream";
        // FileStreamResult em vez de PhysicalFile: não exige volume compartilhado
        // entre réplicas e libera o handle ao fim da resposta.
        var stream = System.IO.File.OpenRead(export.FilePath);
        return File(stream, contentType, await exports.DownloadFileNameAsync(User.UserId(), export));
    }

    /// <summary>
    /// Salva uma resposta de revisão de currículo.
    /// </summary>
    /// <param name="request">Dados da resposta de revisão.</param>
    /// <returns>Resposta salva.</returns>
    [HttpPost("review-answers")]
    public async Task<ActionResult<ResumeReviewAnswerResponse>> SaveReviewAnswer(SaveReviewAnswerRequest request) =>
        Ok(await reviewAnswers.SaveAsync(User.UserId(), request));

    /// <summary>
    /// Lista as respostas de revisão de currículo.
    /// </summary>
    /// <param name="evidenceMapRunId">Identificador opcional do run de evidence map.</param>
    /// <returns>Lista de respostas de revisão.</returns>
    [HttpGet("review-answers")]
    public async Task<ActionResult<ResumeReviewAnswerResponse[]>> ListReviewAnswers([FromQuery] Guid? evidenceMapRunId) =>
        Ok(await reviewAnswers.ListAsync(User.UserId(), evidenceMapRunId));

    /// <summary>
    /// Regenera o currículo após as respostas de revisão.
    /// </summary>
    /// <param name="request">Dados para regeneração.</param>
    /// <returns>Resultado da regeneração.</returns>
    [HttpPost("review-answers/regenerate")]
    public async Task<ActionResult<RegenerateAfterAnswersResponse>> Regenerate(RegenerateAfterAnswersRequest request) =>
        Ok(await reviewAnswers.RegenerateAsync(User.UserId(), request));
}
