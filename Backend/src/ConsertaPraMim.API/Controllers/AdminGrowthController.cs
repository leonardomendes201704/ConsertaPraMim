using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/growth")]
public class AdminGrowthController : ControllerBase
{
    private readonly IAdminGrowthService _adminGrowthService;
    private readonly IAdminLiquidityScoreService _adminLiquidityScoreService;

    public AdminGrowthController(
        IAdminGrowthService adminGrowthService,
        IAdminLiquidityScoreService adminLiquidityScoreService)
    {
        _adminGrowthService = adminGrowthService;
        _adminLiquidityScoreService = adminLiquidityScoreService;
    }

    /// <summary>
    /// Retorna funil operacional de crescimento (pedido -> proposta -> aceite) com medicao de SLA por etapa.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte, em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte, em UTC.</param>
    /// <param name="category">Filtro opcional por categoria (nome, enum ou legacy).</param>
    /// <param name="city">Filtro opcional por cidade.</param>
    /// <param name="proposalSlaMinutes">SLA alvo para primeira proposta, em minutos.</param>
    /// <param name="acceptanceSlaHours">SLA alvo para aceite apos primeira proposta, em horas.</param>
    /// <returns>Payload de funil com estagios, taxas de SLA e alertas operacionais.</returns>
    [HttpGet("funnel")]
    [ProducesResponseType(typeof(AdminGrowthFunnelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFunnel(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] int proposalSlaMinutes = 30,
        [FromQuery] int acceptanceSlaHours = 24)
    {
        var query = new AdminGrowthFunnelQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Category: category,
            City: city,
            ProposalSlaMinutes: proposalSlaMinutes,
            AcceptanceSlaHours: acceptanceSlaHours);

        var response = await _adminGrowthService.GetFunnelAsync(query);
        return Ok(response);
    }

    /// <summary>
    /// Retorna score de liquidez por regiao/categoria com historico diario e alertas de deficit.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte, em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte, em UTC.</param>
    /// <param name="category">Filtro opcional por categoria.</param>
    /// <param name="city">Filtro opcional por cidade.</param>
    /// <param name="proposalSlaMinutes">SLA alvo para primeira proposta, em minutos.</param>
    /// <param name="take">Limite de combinacoes regiao/categoria retornadas no ranking.</param>
    /// <returns>Score de liquidez por combinacao, serie historica e alertas operacionais.</returns>
    [HttpGet("liquidity-score")]
    [ProducesResponseType(typeof(AdminLiquidityScoreResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLiquidityScore(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] int proposalSlaMinutes = 30,
        [FromQuery] int take = 50)
    {
        var response = await _adminLiquidityScoreService.GetScoreAsync(
            new AdminLiquidityScoreQueryDto(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Category: category,
                City: city,
                ProposalSlaMinutes: proposalSlaMinutes,
                Take: take));

        return Ok(response);
    }
}
