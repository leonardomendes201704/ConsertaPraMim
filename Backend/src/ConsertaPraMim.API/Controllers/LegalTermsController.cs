using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/legal-terms")]
public class LegalTermsController : ControllerBase
{
    private readonly ILegalTermsService _legalTermsService;

    public LegalTermsController(ILegalTermsService legalTermsService)
    {
        _legalTermsService = legalTermsService;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] string audience = "client")
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
}
