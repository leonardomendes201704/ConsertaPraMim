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
    /// Envia uma avaliacao do cliente para o prestador.
    /// </summary>
    /// <param name="dto">Nota e comentario da avaliacao.</param>
    /// <response code="200">Avaliacao enviada com sucesso.</response>
    /// <response code="400">Falha ao enviar (pedido nao finalizado ou ja avaliado).</response>
    [Authorize(Roles = "Client")]
    [HttpPost("client")]
    public async Task<IActionResult> SubmitClientReview([FromBody] CreateReviewDto dto)
    {
        var clientIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(clientIdString)) return Unauthorized();

        var clientId = Guid.Parse(clientIdString);
        var success = await _reviewService.SubmitClientReviewAsync(clientId, dto);

        if (!success)
        {
            return BadRequest("Could not submit review. Check if the request is completed, if you are the owner, or if it was already reviewed.");
        }

        return Ok();
    }

    /// <summary>
    /// Envia uma avaliacao do prestador para o cliente.
    /// </summary>
    /// <param name="dto">Nota e comentario da avaliacao.</param>
    /// <response code="200">Avaliacao enviada com sucesso.</response>
    /// <response code="400">Falha ao enviar (pedido nao finalizado, sem proposta aceita ou ja avaliado).</response>
    [Authorize(Roles = "Provider")]
    [HttpPost("provider")]
    public async Task<IActionResult> SubmitProviderReview([FromBody] CreateReviewDto dto)
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(providerIdString)) return Unauthorized();

        var providerId = Guid.Parse(providerIdString);
        var success = await _reviewService.SubmitProviderReviewAsync(providerId, dto);

        if (!success)
        {
            return BadRequest("Could not submit review. Check if the request is completed, if you are the accepted provider, or if it was already reviewed.");
        }

        return Ok();
    }

    // Compat endpoint for older clients. Keeps original behavior (client -> provider).
    [Authorize(Roles = "Client")]
    [HttpPost]
    public Task<IActionResult> Submit([FromBody] CreateReviewDto dto)
    {
        return SubmitClientReview(dto);
    }

    /// <summary>
    /// Lista todas as avaliacoes recebidas por um prestador especifico.
    /// </summary>
    /// <param name="providerId">ID unico do prestador.</param>
    [HttpGet("provider/{providerId}")]
    public async Task<IActionResult> GetByProvider(Guid providerId)
    {
        var reviews = await _reviewService.GetByProviderAsync(providerId);
        return Ok(reviews);
    }
}
