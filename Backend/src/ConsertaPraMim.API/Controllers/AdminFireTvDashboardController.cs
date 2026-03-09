using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/fire-tv/landing-dashboard")]
public sealed class AdminFireTvDashboardController : ControllerBase
{
    private readonly IAdminFireTvDashboardService _service;

    public AdminFireTvDashboardController(IAdminFireTvDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminFireTvLandingDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int? rangeDays = null,
        [FromQuery] string? origin = null,
        [FromQuery] string? comparisonMode = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _service.GetLandingDashboardAsync(rangeDays, origin, comparisonMode, cancellationToken);
        return Ok(response);
    }
}
