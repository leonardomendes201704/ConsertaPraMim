using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Client")]
[ApiController]
[Route("api/client/support-tickets")]
public class ClientSupportTicketsController : ControllerBase
{
    private readonly IClientSupportTicketService _clientSupportTicketService;

    public ClientSupportTicketsController(IClientSupportTicketService clientSupportTicketService)
    {
        _clientSupportTicketService = clientSupportTicketService;
    }

    /// <summary>
    /// Obtem o atendimento contextual de ajuda do cliente para um pedido especifico.
    /// </summary>
    /// <param name="serviceRequestId">Pedido associado ao atendimento.</param>
    /// <returns>Historico do atendimento ligado ao pedido quando existir.</returns>
    [HttpGet("service-requests/{serviceRequestId:guid}")]
    public async Task<IActionResult> GetByServiceRequest(Guid serviceRequestId)
    {
        if (!TryGetCurrentClientUserId(out var clientUserId))
        {
            return Unauthorized();
        }

        var ticket = await _clientSupportTicketService.GetByServiceRequestAsync(clientUserId, serviceRequestId);
        if (ticket == null)
        {
            return NotFound(new
            {
                errorCode = "client_support_ticket_not_found",
                message = "Nao existe atendimento de ajuda para este pedido."
            });
        }

        return Ok(ticket);
    }

    /// <summary>
    /// Registra nova mensagem do cliente no atendimento contextual vinculado ao pedido.
    /// </summary>
    /// <param name="serviceRequestId">Pedido associado ao atendimento.</param>
    /// <param name="request">Mensagem e anexos enviados pelo cliente.</param>
    /// <returns>Snapshot atualizado do atendimento apos a inclusao da mensagem.</returns>
    [HttpPost("service-requests/{serviceRequestId:guid}/messages")]
    public async Task<IActionResult> AddMessage(
        Guid serviceRequestId,
        [FromBody] ClientSupportTicketMessageRequestDto request)
    {
        if (!TryGetCurrentClientUserId(out var clientUserId))
        {
            return Unauthorized();
        }

        var result = await _clientSupportTicketService.AddMessageAsync(clientUserId, serviceRequestId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        if (string.Equals(result.ErrorCode, "client_support_request_not_found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage });
        }

        return BadRequest(new
        {
            errorCode = result.ErrorCode ?? "client_support_message_failed",
            message = result.ErrorMessage ?? "Nao foi possivel registrar a mensagem de suporte."
        });
    }

    private bool TryGetCurrentClientUserId(out Guid clientUserId)
    {
        clientUserId = Guid.Empty;
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out clientUserId);
    }
}
