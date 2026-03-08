using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[Route("api/landing-leads")]
public sealed class LandingLeadsController : ControllerBase
{
    private readonly ILandingLeadService _landingLeadService;

    public LandingLeadsController(ILandingLeadService landingLeadService)
    {
        _landingLeadService = landingLeadService;
    }

    /// <summary>
    /// Captura um lead publico da landing para cliente interessado ou prestador parceiro.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Endpoint publico/anônimo usado pela landing em `www.consertapramim.com`.
    /// - Persiste dados comerciais do contato e metadados tecnicos do browser/requisicao.
    /// - Identifica a origem do lead (`Client` ou `Provider`) para metricas e follow-up operacional.
    /// </remarks>
    /// <param name="request">Payload da captura do lead.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <response code="200">Lead capturado com sucesso.</response>
    /// <response code="400">Payload invalido para captacao do lead.</response>
    [HttpPost("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CaptureLandingLeadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CapturePublicLead(
        [FromBody] CaptureLandingLeadRequestDto request,
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

        var response = await _landingLeadService.CaptureAsync(request, context, cancellationToken);
        return Ok(response);
    }
}
