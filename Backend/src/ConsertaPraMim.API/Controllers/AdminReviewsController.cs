using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public AdminReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Lista fila de avaliacoes denunciadas e pendentes de moderacao.
    /// </summary>
    /// <remarks>
    /// Endpoint operacional para o painel de moderacao do admin.
    /// Cada item inclui contexto da denuncia e estado de moderacao.
    /// </remarks>
    /// <response code="200">Fila retornada com sucesso.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Admin.</response>
    [HttpGet("reported")]
    public async Task<IActionResult> GetReported()
    {
        var reviews = await _reviewService.GetReportedReviewsAsync();
        return Ok(reviews);
    }

    /// <summary>
    /// Aplica decisao de moderacao em uma avaliacao denunciada.
    /// </summary>
    /// <remarks>
    /// Decisoes suportadas em <c>decision</c>:
    /// - <c>KeepVisible</c>: mantem comentario visivel e encerra denuncia.
    /// - <c>HideComment</c>: oculta comentario publicamente e encerra denuncia.
    /// </remarks>
    /// <param name="reviewId">Identificador da avaliacao denunciada.</param>
    /// <param name="dto">Payload com decisao e justificativa opcional.</param>
    /// <response code="200">Moderacao aplicada com sucesso.</response>
    /// <response code="400">Decisao invalida ou review fora de estado elegivel para moderacao.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Admin.</response>
    [HttpPost("{reviewId:guid}/moderate")]
    public async Task<IActionResult> Moderate(Guid reviewId, [FromBody] ModerateReviewDto dto)
    {
        var adminIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(adminIdString) || !Guid.TryParse(adminIdString, out var adminId))
        {
            return Unauthorized();
        }

        var success = await _reviewService.ModerateReviewAsync(reviewId, adminId, dto);
        if (!success)
        {
            return BadRequest("Could not moderate review. Check decision value and review moderation state.");
        }

        return Ok();
    }
}
