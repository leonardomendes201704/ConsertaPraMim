using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/google-calendar-sync")]
public sealed class AdminGoogleCalendarSyncController : ControllerBase
{
    private readonly IGoogleCalendarSyncOperationsService _googleCalendarSyncOperationsService;

    public AdminGoogleCalendarSyncController(IGoogleCalendarSyncOperationsService googleCalendarSyncOperationsService)
    {
        _googleCalendarSyncOperationsService = googleCalendarSyncOperationsService;
    }

    /// <summary>
    /// Retorna visao operacional da sincronizacao de agendamentos com Google Calendar.
    /// </summary>
    /// <param name="fromUtc">Inicio opcional do periodo em UTC.</param>
    /// <param name="toUtc">Fim opcional do periodo em UTC.</param>
    /// <returns>Resumo com volume por status, fila de retry e latencias.</returns>
    /// <response code="200">Resumo retornado com sucesso.</response>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        var result = await _googleCalendarSyncOperationsService.GetOverviewAsync(fromUtc, toUtc, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Reprocessa sincronizacoes pendentes/falhas por appointmentId ou intervalo.
    /// </summary>
    /// <param name="request">Filtro de reprocessamento manual.</param>
    /// <returns>Resultado item a item com sucesso/falha/dead-letter.</returns>
    /// <response code="200">Reprocessamento executado.</response>
    /// <response code="400">Payload invalido.</response>
    [HttpPost("reprocess")]
    public async Task<IActionResult> Reprocess([FromBody] GoogleCalendarSyncReprocessRequestDto request)
    {
        if (request.MaxItems is < 1 or > 2000)
        {
            return BadRequest(new
            {
                errorMessage = "MaxItems deve estar entre 1 e 2000.",
                errorCode = "validation_error"
            });
        }

        var result = await _googleCalendarSyncOperationsService.ReprocessAsync(request, HttpContext.RequestAborted);
        return Ok(result);
    }
}
