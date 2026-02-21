using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminDashboardController(IAdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    /// <summary>
    /// Retorna metricas consolidadas do dashboard administrativo, incluindo operacao, financeiro,
    /// reputacao (ranking de clientes/prestadores), outliers de reviews e KPIs operacionais de agenda
    /// (confirmacao no SLA, reagendamento, cancelamento e falha de lembretes).
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional em UTC para o recorte do painel.</param>
    /// <param name="toUtc">Data final opcional em UTC para o recorte do painel.</param>
    /// <param name="eventType">Filtro de eventos: all, request, proposal, chat.</param>
    /// <param name="operationalStatus">Filtro opcional por status operacional do atendimento (OnTheWay, OnSite, InService, WaitingParts, Completed).</param>
    /// <param name="searchTerm">Termo de busca textual aplicado em titulo/descricao dos eventos.</param>
    /// <param name="page">Pagina de eventos recentes (inicio em 1).</param>
    /// <param name="pageSize">Tamanho de pagina dos eventos recentes (maximo 100).</param>
    /// <returns>DTO completo de dashboard com KPIs, breakdowns, ranking de reputacao e outliers.</returns>
    /// <response code="200">Dashboard retornado com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao administrativa.</response>
    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? eventType,
        [FromQuery] string? operationalStatus,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminDashboardQueryDto(fromUtc, toUtc, eventType, operationalStatus, searchTerm, page, pageSize);
        var response = await _adminDashboardService.GetDashboardAsync(query);
        return Ok(response);
    }

    /// <summary>
    /// Retorna dados geograficos para mapa operacional no admin:
    /// prestadores com base/radio de atuacao e pedidos com localizacao.
    /// </summary>
    /// <returns>Payload de mapa com pedidos e prestadores.</returns>
    /// <response code="200">Mapa retornado com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao administrativa.</response>
    [HttpGet("coverage-map")]
    public async Task<IActionResult> GetCoverageMap()
    {
        var response = await _adminDashboardService.GetCoverageMapAsync();
        return Ok(response);
    }
}
