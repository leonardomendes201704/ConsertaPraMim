using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/legal-terms")]
public class AdminLegalTermsController : ControllerBase
{
    private readonly ILegalTermsService _legalTermsService;

    public AdminLegalTermsController(ILegalTermsService legalTermsService)
    {
        _legalTermsService = legalTermsService;
    }

    [HttpGet("{audience}/active")]
    public async Task<IActionResult> GetActive([FromRoute] string audience)
    {
        if (!LegalTermsService.TryParseAudience(audience, out var parsedAudience))
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "legal_terms_invalid_audience",
                errorMessage = "Audience invalido. Use client ou provider."
            });
        }

        var active = await _legalTermsService.GetActiveAsync(parsedAudience, HttpContext.RequestAborted);
        if (active == null)
        {
            return NotFound(new
            {
                success = false,
                errorCode = "legal_terms_not_found",
                errorMessage = "Termo ativo nao encontrado para o publico informado."
            });
        }

        return Ok(active);
    }

    [HttpGet("{audience}/versions")]
    public async Task<IActionResult> GetVersions([FromRoute] string audience)
    {
        if (!LegalTermsService.TryParseAudience(audience, out var parsedAudience))
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "legal_terms_invalid_audience",
                errorMessage = "Audience invalido. Use client ou provider."
            });
        }

        var versions = await _legalTermsService.GetVersionsAsync(parsedAudience, HttpContext.RequestAborted);
        return Ok(versions);
    }

    [HttpPost("{audience}/publish")]
    public async Task<IActionResult> Publish(
        [FromRoute] string audience,
        [FromBody] LegalTermsPublishPayloadDto payload)
    {
        if (!LegalTermsService.TryParseAudience(audience, out var parsedAudience))
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "legal_terms_invalid_audience",
                errorMessage = "Audience invalido. Use client ou provider."
            });
        }

        var result = await _legalTermsService.PublishAsync(
            parsedAudience,
            payload,
            ResolveActorUserId(),
            ResolveActorEmail(),
            HttpContext.RequestAborted);

        if (!result.Success || result.Document == null)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = result.ErrorCode ?? "legal_terms_publish_failed",
                errorMessage = result.ErrorMessage ?? "Falha ao publicar termo."
            });
        }

        return Ok(new
        {
            success = true,
            document = result.Document
        });
    }

    private Guid ResolveActorUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.Empty;
    }

    private string? ResolveActorEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ??
               User.Identity?.Name;
    }
}
