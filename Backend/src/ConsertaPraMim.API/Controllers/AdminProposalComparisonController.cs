using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/proposal-comparison")]
public class AdminProposalComparisonController : ControllerBase
{
    private readonly IMobileClientOrderService _mobileClientOrderService;

    public AdminProposalComparisonController(IMobileClientOrderService mobileClientOrderService)
    {
        _mobileClientOrderService = mobileClientOrderService;
    }

    /// <summary>
    /// Retorna consolidado A/B do comparador de propostas no periodo.
    /// </summary>
    /// <remarks>
    /// O endpoint agrupa telemetria por bucket de experimento (`control` e `variant`) e entrega:
    /// views, trocas de ordenacao, aberturas de proposta, aceites apos comparacao e taxa de conversao.
    /// </remarks>
    /// <param name="fromUtc">Inicio da janela em UTC (padrao: agora - 30 dias).</param>
    /// <param name="toUtc">Fim da janela em UTC (padrao: agora).</param>
    /// <response code="200">Resumo retornado com sucesso.</response>
    /// <response code="401">Token invalido/ausente.</response>
    /// <response code="403">Usuario sem permissao administrativa.</response>
    [HttpGet("ab-summary")]
    [ProducesResponseType(typeof(MobileClientProposalComparisonAbSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAbSummary([FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var effectiveToUtc = toUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var effectiveFromUtc = fromUtc?.ToUniversalTime() ?? effectiveToUtc.AddDays(-30);

        var result = await _mobileClientOrderService.GetProposalComparisonAbSummaryAsync(effectiveFromUtc, effectiveToUtc);
        return Ok(result);
    }
}
