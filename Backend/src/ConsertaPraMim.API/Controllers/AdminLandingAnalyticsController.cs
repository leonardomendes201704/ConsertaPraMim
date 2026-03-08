using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/landing-analytics")]
public sealed class AdminLandingAnalyticsController : ControllerBase
{
    private readonly IAdminLandingAnalyticsService _adminLandingAnalyticsService;

    public AdminLandingAnalyticsController(IAdminLandingAnalyticsService adminLandingAnalyticsService)
    {
        _adminLandingAnalyticsService = adminLandingAnalyticsService;
    }

    /// <summary>
    /// Retorna o overview operacional da analytics da landing publica.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AdminLandingAnalyticsOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] string? searchTerm,
        [FromQuery] string? origin,
        [FromQuery] string? path,
        [FromQuery] string? countryCode,
        [FromQuery] string? region,
        [FromQuery] string? city,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminLandingAnalyticsQueryDto(
            searchTerm,
            origin,
            path,
            countryCode,
            region,
            city,
            fromUtc,
            toUtc,
            page,
            pageSize);

        var response = await _adminLandingAnalyticsService.GetOverviewAsync(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retorna o detalhe correlacionado de uma sessao da landing.
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(AdminLandingAnalyticsSessionDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionDetails(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminLandingAnalyticsService.GetSessionDetailsAsync(sessionId, cancellationToken);
        return response == null ? NotFound() : Ok(response);
    }
}
