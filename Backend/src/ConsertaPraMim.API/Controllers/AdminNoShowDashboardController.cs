using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
