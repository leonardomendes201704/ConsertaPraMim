using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Envia uma avaliação para um serviço finalizado (Apenas o Cliente).
    /// </summary>
    /// <param name="dto">Nota e comentário da avaliação.</param>
    /// <response code="200">Avaliação enviada com sucesso.</response>
    /// <response code="400">Falha ao enviar (pedido não finalizado ou já avaliado).</response>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] CreateReviewDto dto)
    {
        var clientIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(clientIdString)) return Unauthorized();

        var clientId = Guid.Parse(clientIdString);
        var success = await _reviewService.SubmitReviewAsync(clientId, dto);
        
        if (!success) return BadRequest("Could not submit review. Check if the request is completed, if you are the owner, or if it was already reviewed.");
        
        return Ok();
    }

    /// <summary>
    /// Lista todas as avaliações recebidas por um prestador específico.
    /// </summary>
    /// <param name="providerId">ID único do prestador.</param>
    [HttpGet("provider/{providerId}")]
    public async Task<IActionResult> GetByProvider(Guid providerId)
    {
        var reviews = await _reviewService.GetByProviderAsync(providerId);
        return Ok(reviews);
    }
}
