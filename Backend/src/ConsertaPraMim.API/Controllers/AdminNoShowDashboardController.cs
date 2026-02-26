using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/no-show-dashboard")]
public class AdminNoShowDashboardController : ControllerBase
{
    private readonly IAdminNoShowDashboardService _adminNoShowDashboardService;

    public AdminNoShowDashboardController(IAdminNoShowDashboardService adminNoShowDashboardService)
    {
        _adminNoShowDashboardService = adminNoShowDashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] AdminNoShowDashboardQueryDto query)
    {
        var dashboard = await _adminNoShowDashboardService.GetDashboardAsync(query);
        return Ok(dashboard);
    }

    /// <summary>
    /// Retorna um KPI isolado do painel de no-show para carregamento incremental na home admin.
    /// </summary>
    /// <param name="kpiKey">Identificador do KPI (`no-show-rate`, `attendance-rate`, `dual-confirmation-rate`, `high-risk`, `queue`, `client-recurrence`, `provider-recurrence`, `critical-clients`, `critical-providers`).</param>
    /// <param name="query">Mesmo recorte aplicado no painel de no-show.</param>
    /// <returns>KPI isolado do painel operacional.</returns>
    /// <response code="200">KPI retornado com sucesso.</response>
    /// <response code="404">KPI informado nao existe ou nao e suportado.</response>
    [HttpGet("kpis/{kpiKey}")]
    public async Task<IActionResult> GetKpi([FromRoute] string kpiKey, [FromQuery] AdminNoShowDashboardQueryDto query)
    {
        try
        {
            var card = await _adminNoShowDashboardService.GetKpiAsync(query, kpiKey);
            return Ok(card);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorMessage = ex.Message });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportDashboard([FromQuery] AdminNoShowDashboardQueryDto query)
    {
        var csv = await _adminNoShowDashboardService.ExportDashboardCsvAsync(query);
        var fileName = $"admin-no-show-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }
}
