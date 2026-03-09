using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/fire-tv/operations-dashboard")]
public sealed class AdminFireTvOperationsDashboardController : ControllerBase
{
    private readonly IAdminFireTvDashboardService _service;

    public AdminFireTvOperationsDashboardController(IAdminFireTvDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminFireTvOperationsDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var response = await _service.GetOperationsDashboardAsync(cancellationToken);
        return Ok(response);
    }
}
