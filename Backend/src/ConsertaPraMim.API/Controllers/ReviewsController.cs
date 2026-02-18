using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// Registra avaliacao do cliente para o prestador vencedor do pedido.
    /// </summary>
    /// <remarks>
    /// Regras de negocio:
    /// - Exige token de usuario com role Client.
    /// - So permite avaliar pedidos concluidos/validados e com pagamento confirmado.
    /// - Respeita janela de avaliacao configurada (Reviews:EvaluationWindowDays).
    /// - Impede avaliacao duplicada pelo mesmo revisor no mesmo pedido.
    /// </remarks>
    /// <param name="dto">Dados da avaliacao (requestId, rating de 1 a 5, comentario).</param>
    /// <response code="200">Avaliacao registrada com sucesso.</response>
    /// <response code="400">Falha de regra de negocio (pedido inelegivel, janela expirada, duplicidade, etc).</response>
    /// <response code="401">Usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [Authorize(Roles = "Client")]
    [HttpPost("client")]
    public async Task<IActionResult> SubmitClientReview([FromBody] CreateReviewDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var clientId = Guid.Parse(userIdString);
        var success = await _reviewService.SubmitClientReviewAsync(clientId, dto);

        if (!success)
        {
            return BadRequest("Could not submit review. Check eligibility, payment status, review window and duplicate submissions.");
        }

        return Ok();
    }

    /// <summary>
    /// Registra avaliacao do prestador para o cliente do pedido.
    /// </summary>
    /// <remarks>
    /// Regras de negocio:
    /// - Exige token de usuario com role Provider.
    /// - Prestador deve ser o prestador com proposta aceita no pedido.
    /// - So permite avaliar pedidos concluidos/validados e com pagamento confirmado.
    /// - Respeita janela de avaliacao configurada e impede duplicidade por revisor.
    /// </remarks>
    /// <param name="dto">Dados da avaliacao (requestId, rating de 1 a 5, comentario).</param>
    /// <response code="200">Avaliacao registrada com sucesso.</response>
    /// <response code="400">Falha de regra de negocio (pedido inelegivel, prestador invalido, duplicidade, etc).</response>
    /// <response code="401">Usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [Authorize(Roles = "Provider")]
    [HttpPost("provider")]
    public async Task<IActionResult> SubmitProviderReview([FromBody] CreateReviewDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var providerId = Guid.Parse(userIdString);
        var success = await _reviewService.SubmitProviderReviewAsync(providerId, dto);

        if (!success)
        {
            return BadRequest("Could not submit review. Check eligibility, accepted provider constraint, payment status and duplicate submissions.");
        }

        return Ok();
    }

    /// <summary>
    /// Endpoint legado para avaliacao de cliente para prestador.
    /// </summary>
    /// <remarks>
    /// Mantido por compatibilidade retroativa. Novo fluxo recomendado: POST /api/reviews/client.
    /// </remarks>
    /// <param name="dto">Dados da avaliacao.</param>
    /// <response code="200">Avaliacao registrada.</response>
    /// <response code="400">Falha de regra de negocio.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [Authorize(Roles = "Client")]
    [HttpPost]
    public Task<IActionResult> Submit([FromBody] CreateReviewDto dto)
    {
        return SubmitClientReview(dto);
    }

    /// <summary>
    /// Lista avaliacoes recebidas por um prestador.
    /// </summary>
    /// <remarks>
    /// Retorna comentarios ja tratados por moderacao quando aplicavel.
    /// </remarks>
    /// <param name="providerId">Identificador do prestador avaliado.</param>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    [HttpGet("provider/{providerId:guid}")]
    public async Task<IActionResult> GetByProvider(Guid providerId)
    {
        var reviews = await _reviewService.GetByProviderAsync(providerId);
        return Ok(reviews);
    }

    [HttpGet("client/{clientId:guid}")]
    public async Task<IActionResult> GetByClient(Guid clientId)
    {
        var reviews = await _reviewService.GetByClientAsync(clientId);
        return Ok(reviews);
    }

    /// <summary>
    /// Retorna media e distribuicao de notas recebidas por um prestador.
    /// </summary>
    /// <param name="providerId">Identificador do prestador avaliado.</param>
    /// <response code="200">Resumo calculado com sucesso.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    [HttpGet("summary/provider/{providerId:guid}")]
    public async Task<IActionResult> GetProviderSummary(Guid providerId)
    {
        var summary = await _reviewService.GetProviderScoreSummaryAsync(providerId);
        return Ok(summary);
    }

    /// <summary>
    /// Retorna media e distribuicao de notas recebidas por um cliente.
    /// </summary>
    /// <param name="clientId">Identificador do cliente avaliado.</param>
    /// <response code="200">Resumo calculado com sucesso.</response>
    /// <response code="401">Usuario nao autenticado.</response>
    [HttpGet("summary/client/{clientId:guid}")]
    public async Task<IActionResult> GetClientSummary(Guid clientId)
    {
        var summary = await _reviewService.GetClientScoreSummaryAsync(clientId);
        return Ok(summary);
    }

    /// <summary>
    /// Registra denuncia de comentario de avaliacao para moderacao.
    /// </summary>
    /// <remarks>
    /// Regras:
    /// - Disponivel para Client, Provider e Admin autenticados.
    /// - Nao permite denunciar avaliacao inexistente.
    /// - Nao permite denunciar propria avaliacao.
    /// - Nao permite duplicar denuncia pendente da mesma avaliacao.
    /// </remarks>
    /// <param name="reviewId">Identificador da avaliacao denunciada.</param>
    /// <param name="dto">Motivo da denuncia.</param>
    /// <response code="200">Denuncia registrada com sucesso.</response>
    /// <response code="400">Denuncia invalida (regra de negocio nao atendida).</response>
    /// <response code="401">Usuario nao autenticado.</response>
    [HttpPost("{reviewId:guid}/report")]
    public async Task<IActionResult> Report(Guid reviewId, [FromBody] ReportReviewDto dto)
    {
        if (!TryGetCurrentUserContext(out var userId, out var role))
        {
            return Unauthorized();
        }

        var success = await _reviewService.ReportReviewAsync(reviewId, userId, role, dto);
        if (!success)
        {
            return BadRequest("Could not report this review. Check reason, permissions and current moderation status.");
        }

        return Ok();
    }

    private bool TryGetCurrentUserContext(out Guid userId, out UserRole role)
    {
        userId = Guid.Empty;
        role = default;

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleString = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrWhiteSpace(userIdString) ||
            string.IsNullOrWhiteSpace(roleString) ||
            !Guid.TryParse(userIdString, out userId) ||
            !Enum.TryParse<UserRole>(roleString, true, out role))
        {
            return false;
        }

        return true;
    }
}
