using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Operacoes administrativas da carteira de pacotes PJ recorrentes.
/// </summary>
/// <remarks>
/// Exibe visao consolidada dos contratos recorrentes PJ para governanca comercial e operacional:
/// - volume por status/categoria;
/// - receita mensal recorrente;
/// - tabela de contratos com dados de renovacao, SLA e elegibilidade.
/// </remarks>
[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/pj-recurring-contracts")]
public class AdminPjRecurringContractsController : ControllerBase
{
    private readonly IPjRecurringContractService _pjRecurringContractService;

    public AdminPjRecurringContractsController(IPjRecurringContractService pjRecurringContractService)
    {
        _pjRecurringContractService = pjRecurringContractService;
    }

    /// <summary>
    /// Retorna carteira consolidada de contratos PJ recorrentes.
    /// </summary>
    /// <param name="fromUtc">Inicio opcional da janela de consulta em UTC (base em CreatedAt).</param>
    /// <param name="toUtc">Fim opcional da janela de consulta em UTC (base em CreatedAt).</param>
    /// <param name="status">Filtro opcional por status do contrato.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Painel de carteira PJ para operacao admin.</returns>
    [HttpGet("portfolio")]
    [ProducesResponseType(typeof(AdminPjRecurringPortfolioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPortfolio(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] PjRecurringContractStatus? status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var portfolio = await _pjRecurringContractService.GetAdminPortfolioAsync(
                fromUtc,
                toUtc,
                status,
                cancellationToken);
            return Ok(portfolio);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "pj_recurring_portfolio_invalid_query", errorMessage = ex.Message });
        }
    }
}
