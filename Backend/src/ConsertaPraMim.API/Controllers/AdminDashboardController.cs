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
    /// Retorna metricas consolidadas do painel administrativo com filtros basicos e eventos paginados.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional em UTC.</param>
    /// <param name="toUtc">Data final opcional em UTC.</param>
    /// <param name="eventType">Filtro opcional de tipo de evento: all, request, proposal, chat.</param>
    /// <param name="operationalStatus">Filtro opcional de status operacional do atendimento.</param>
    /// <param name="searchTerm">Filtro textual opcional para eventos.</param>
    /// <param name="page">Pagina (inicia em 1).</param>
    /// <param name="pageSize">Quantidade de itens por pagina.</param>
    /// <returns>Resumo de metricas globais e eventos recentes.</returns>
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
}
