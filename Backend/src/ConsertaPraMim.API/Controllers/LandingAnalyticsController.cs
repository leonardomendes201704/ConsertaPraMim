using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[Route("api/landing-analytics")]
public sealed class LandingAnalyticsController : ControllerBase
{
    private readonly ILandingAnalyticsRuntimeSettings _landingAnalyticsRuntimeSettings;
    private readonly ILandingTelemetryEventService _landingTelemetryEventService;

    public LandingAnalyticsController(
        ILandingAnalyticsRuntimeSettings landingAnalyticsRuntimeSettings,
        ILandingTelemetryEventService landingTelemetryEventService)
    {
        _landingAnalyticsRuntimeSettings = landingAnalyticsRuntimeSettings;
        _landingTelemetryEventService = landingTelemetryEventService;
    }

    /// <summary>
    /// Retorna a configuracao publica da telemetria da landing para o browser.
    /// </summary>
    [HttpGet("public/config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LandingAnalyticsPublicConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicConfig(CancellationToken cancellationToken)
    {
        var config = await _landingAnalyticsRuntimeSettings.GetPublicConfigAsync(cancellationToken);
        return Ok(config);
    }

    /// <summary>
    /// Registra eventos comportamentais fase 1 da landing publica.
    /// </summary>
    [HttpPost("public/events")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecordLandingTelemetryBatchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPublicEvents(
        [FromBody] RecordLandingTelemetryBatchRequestDto request,
        CancellationToken cancellationToken)
    {
        var context = new LandingLeadCaptureContextDto(
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            ForwardedFor: Request.Headers["X-Forwarded-For"].ToString(),
            UserAgent: Request.Headers.UserAgent.ToString(),
            AcceptLanguage: Request.Headers.AcceptLanguage.ToString(),
            Host: Request.Host.Value,
            Scheme: Request.Scheme,
            Path: Request.Path.Value,
            RefererHeader: Request.Headers.Referer.ToString());

        var response = await _landingTelemetryEventService.RecordBatchAsync(request, context, cancellationToken);
        return Ok(response);
    }
}
