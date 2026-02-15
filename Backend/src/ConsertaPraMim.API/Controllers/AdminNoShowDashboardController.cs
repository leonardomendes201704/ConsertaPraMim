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

    [HttpGet("export")]
    public async Task<IActionResult> ExportDashboard([FromQuery] AdminNoShowDashboardQueryDto query)
    {
        var csv = await _adminNoShowDashboardService.ExportDashboardCsvAsync(query);
        var fileName = $"admin-no-show-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }
}
