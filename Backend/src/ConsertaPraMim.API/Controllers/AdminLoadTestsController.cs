using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/loadtests")]
public class AdminLoadTestsController : ControllerBase
{
    private readonly IAdminLoadTestRunService _adminLoadTestRunService;

    public AdminLoadTestsController(IAdminLoadTestRunService adminLoadTestRunService)
    {
        _adminLoadTestRunService = adminLoadTestRunService;
    }

    /// <summary>
    /// Lista execucoes de teste de carga importadas no ambiente admin.
    /// </summary>
    /// <param name="scenario">Filtro opcional por cenario (smoke, baseline, stress...).</param>
    /// <param name="fromUtc">Inicio da janela em UTC.</param>
    /// <param name="toUtc">Fim da janela em UTC.</param>
    /// <param name="search">Busca textual em runId, cenario, baseUrl e source.</param>
    /// <param name="page">Pagina (inicio em 1).</param>
    /// <param name="pageSize">Tamanho da pagina (1 a 200).</param>
    /// <returns>Lista paginada de runs de carga.</returns>
    /// <response code="200">Runs retornados com sucesso.</response>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(
        [FromQuery] string? scenario = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _adminLoadTestRunService.GetRunsAsync(
            new AdminLoadTestRunsQueryDto(
                scenario,
                fromUtc?.ToUniversalTime(),
                toUtc?.ToUniversalTime(),
                search,
                page,
                pageSize),
            HttpContext.RequestAborted);

        return Ok(result);
    }

    /// <summary>
    /// Retorna o detalhe completo de uma execucao de teste de carga.
    /// </summary>
    /// <param name="id">Id interno do run persistido.</param>
    /// <returns>Detalhes, snapshots e payload bruto do report.</returns>
    /// <response code="200">Detalhe retornado.</response>
    /// <response code="404">Run nao encontrado.</response>
    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetRunById([FromRoute] Guid id)
    {
        var result = await _adminLoadTestRunService.GetRunByIdAsync(id, HttpContext.RequestAborted);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Importa (ou atualiza) um report de teste de carga para analise no portal admin.
    /// </summary>
    /// <param name="request">Payload contendo source opcional e objeto report do runner.</param>
    /// <returns>Resultado da importacao com id e runId externo.</returns>
    /// <response code="200">Importacao processada com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] AdminLoadTestImportRequestDto request)
    {
        try
        {
            var result = await _adminLoadTestRunService.ImportRunAsync(request, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

