using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do cliente para consulta de pedidos.
/// </summary>
/// <remarks>
/// Este controlador foi criado para isolar o contrato do app cliente dos contratos usados pelos portais Web.
/// Assim, evolucoes no app mobile nao quebram as telas de backoffice/prestador/cliente web.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/mobile/client/orders")]
public class MobileClientOrdersController : ControllerBase
{
    private readonly IMobileClientOrderService _mobileClientOrderService;

    public MobileClientOrdersController(IMobileClientOrderService mobileClientOrderService)
    {
        _mobileClientOrderService = mobileClientOrderService;
    }

    /// <summary>
    /// Retorna os pedidos do cliente autenticado separados por grupos operacionais do app.
    /// </summary>
    /// <param name="takePerBucket">
    /// Quantidade maxima por grupo retornado.
    /// O endpoint retorna dois grupos: <c>openOrders</c> (nao finalizados) e <c>finalizedOrders</c> (finalizados/cancelados).
    /// </param>
    /// <returns>
    /// Payload orientado ao app com listas separadas para renderizacao das abas "Ativos" e "Historico".
    /// Cada item inclui <c>proposalCount</c> para exibir badge de quantidade de propostas recebidas no pedido.
    /// </returns>
    /// <response code="200">Pedidos retornados com sucesso.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpGet]
    [ProducesResponseType(typeof(MobileClientOrdersResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyOrders([FromQuery] int takePerBucket = 100)
    {
        var clientUserId = TryGetClientUserId();
        if (!clientUserId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_orders_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var response = await _mobileClientOrderService.GetMyOrdersAsync(clientUserId.Value, takePerBucket);
        return Ok(response);
    }

    /// <summary>
    /// Retorna os detalhes de um pedido especifico do cliente autenticado com acompanhamento historico e fluxo operacional.
    /// </summary>
    /// <param name="orderId">Identificador do pedido que deve ser exibido na tela de detalhes do app.</param>
    /// <returns>
    /// Payload com resumo do pedido (incluindo <c>proposalCount</c>), etapas do fluxo atual e timeline historica de eventos.
    /// </returns>
    /// <response code="200">Detalhes retornados com sucesso.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Pedido nao encontrado para o cliente autenticado.</response>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderDetails([FromRoute] Guid orderId)
    {
        var clientUserId = TryGetClientUserId();
        if (!clientUserId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_orders_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var details = await _mobileClientOrderService.GetOrderDetailsAsync(clientUserId.Value, orderId);
        if (details == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_order_not_found",
                message = "Pedido nao encontrado para o cliente autenticado."
            });
        }

        return Ok(details);
    }

    /// <summary>
    /// Retorna os detalhes de uma proposta recebida em um pedido do cliente autenticado.
    /// </summary>
    /// <param name="orderId">Identificador do pedido dono da proposta.</param>
    /// <param name="proposalId">Identificador da proposta referenciada no historico/timeline do pedido.</param>
    /// <returns>
    /// Payload dedicado para a tela mobile de detalhe da proposta, contendo:
    /// <list type="bullet">
    /// <item><description>Resumo do pedido para contexto;</description></item>
    /// <item><description>Prestador, valor estimado, mensagem e status comercial da proposta.</description></item>
    /// </list>
    /// </returns>
    /// <response code="200">Detalhes da proposta retornados com sucesso.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Pedido/proposta nao encontrado para o cliente autenticado.</response>
    [HttpGet("{orderId:guid}/proposals/{proposalId:guid}")]
    [ProducesResponseType(typeof(MobileClientOrderProposalDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderProposalDetails([FromRoute] Guid orderId, [FromRoute] Guid proposalId)
    {
        var clientUserId = TryGetClientUserId();
        if (!clientUserId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_orders_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var details = await _mobileClientOrderService.GetOrderProposalDetailsAsync(clientUserId.Value, orderId, proposalId);
        if (details == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_order_proposal_not_found",
                message = "Proposta nao encontrada para o pedido do cliente autenticado."
            });
        }

        return Ok(details);
    }

    /// <summary>
    /// Aceita uma proposta do pedido do cliente autenticado no fluxo mobile.
    /// </summary>
    /// <param name="orderId">Identificador do pedido.</param>
    /// <param name="proposalId">Identificador da proposta a ser aceita.</param>
    /// <returns>
    /// Retorna resumo atualizado do pedido e da proposta apos aceite, para sincronizar a UI do app sem refresh completo.
    /// </returns>
    /// <response code="200">Proposta aceita com sucesso.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Pedido/proposta nao encontrado para o cliente autenticado.</response>
    [HttpPost("{orderId:guid}/proposals/{proposalId:guid}/accept")]
    [ProducesResponseType(typeof(MobileClientAcceptProposalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptOrderProposal([FromRoute] Guid orderId, [FromRoute] Guid proposalId)
    {
        var clientUserId = TryGetClientUserId();
        if (!clientUserId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_orders_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var result = await _mobileClientOrderService.AcceptProposalAsync(clientUserId.Value, orderId, proposalId);
        if (result == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_order_proposal_not_found",
                message = "Nao foi possivel aceitar esta proposta para o pedido informado."
            });
        }

        return Ok(result);
    }

    private Guid? TryGetClientUserId()
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out var userId) ? userId : null;
    }
}
