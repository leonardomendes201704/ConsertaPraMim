using System.Security.Claims;
using System.Globalization;
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
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public MobileClientOrdersController(
        IMobileClientOrderService mobileClientOrderService,
        IServiceAppointmentService serviceAppointmentService)
    {
        _mobileClientOrderService = mobileClientOrderService;
        _serviceAppointmentService = serviceAppointmentService;
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

    /// <summary>
    /// Retorna horarios disponiveis para agendamento da proposta aceita no app cliente.
    /// </summary>
    /// <remarks>
    /// Endpoint dedicado ao app mobile/web cliente para manter isolamento de contrato em relacao aos portais.
    ///
    /// Regras:
    /// <list type="bullet">
    /// <item><description>O pedido/proposta precisa pertencer ao cliente autenticado.</description></item>
    /// <item><description>A proposta precisa estar aceita e valida (nao invalidada).</description></item>
    /// <item><description>Quando ja existe agendamento ativo para a proposta, nova solicitacao de slot e bloqueada.</description></item>
    /// <item><description>A data deve ser enviada em formato <c>yyyy-MM-dd</c>.</description></item>
    /// <item><description>Os slots sao calculados com a mesma regra operacional do portal do cliente.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="orderId">Identificador do pedido.</param>
    /// <param name="proposalId">Identificador da proposta aceita para agendamento.</param>
    /// <param name="date">Data alvo para consulta de slots no formato <c>yyyy-MM-dd</c>.</param>
    /// <param name="slotDurationMinutes">Duracao opcional do slot (entre 15 e 240 minutos).</param>
    /// <returns>Lista de janelas disponiveis para o prestador da proposta.</returns>
    /// <response code="200">Slots retornados com sucesso.</response>
    /// <response code="400">Parametros invalidos (data/intervalo/duracao).</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Pedido/proposta nao encontrado para o cliente autenticado.</response>
    /// <response code="409">Conflito de regra de negocio (proposta nao aceita, slot indisponivel, etc.).</response>
    [HttpGet("{orderId:guid}/proposals/{proposalId:guid}/schedule/slots")]
    [ProducesResponseType(typeof(MobileClientOrderProposalSlotsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetProposalScheduleSlots(
        [FromRoute] Guid orderId,
        [FromRoute] Guid proposalId,
        [FromQuery] string date,
        [FromQuery] int? slotDurationMinutes = null)
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

        var proposalDetails = await _mobileClientOrderService.GetOrderProposalDetailsAsync(clientUserId.Value, orderId, proposalId);
        if (proposalDetails == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_order_proposal_not_found",
                message = "Proposta nao encontrada para o pedido do cliente autenticado."
            });
        }

        if (!proposalDetails.Proposal.Accepted || proposalDetails.Proposal.Invalidated)
        {
            return Conflict(new
            {
                errorCode = "mobile_client_order_proposal_not_accepted",
                message = "Para agendar, a proposta precisa estar aceita e valida."
            });
        }

        if (proposalDetails.CurrentAppointment != null && IsAppointmentBlockingForNewSchedule(proposalDetails.CurrentAppointment.Status))
        {
            return Conflict(new
            {
                errorCode = "mobile_client_order_proposal_already_scheduled",
                message = "Ja existe um agendamento solicitado para esta proposta. Aguarde a confirmacao do prestador."
            });
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return BadRequest(new
            {
                errorCode = "mobile_client_invalid_date",
                message = "Data invalida para consulta de horarios. Use o formato yyyy-MM-dd."
            });
        }

        var dayStartLocal = DateTime.SpecifyKind(parsedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var fromUtc = dayStartLocal.ToUniversalTime();
        var toUtc = fromUtc.AddDays(1);

        var slotsResult = await _serviceAppointmentService.GetAvailableSlotsAsync(
            clientUserId.Value,
            "Client",
            new GetServiceAppointmentSlotsQueryDto(
                proposalDetails.Proposal.ProviderId,
                fromUtc,
                toUtc,
                slotDurationMinutes));

        if (!slotsResult.Success)
        {
            return MapAppointmentFailure(slotsResult.ErrorCode, slotsResult.ErrorMessage);
        }

        var payload = new MobileClientOrderProposalSlotsResponseDto(
            orderId,
            proposalId,
            proposalDetails.Proposal.ProviderId,
            parsedDate,
            slotsResult.Slots
                .Select(slot => new MobileClientOrderProposalSlotDto(slot.WindowStartUtc, slot.WindowEndUtc))
                .ToList());

        return Ok(payload);
    }

    /// <summary>
    /// Solicita agendamento para uma proposta aceita no app cliente.
    /// </summary>
    /// <remarks>
    /// Regras:
    /// <list type="bullet">
    /// <item><description>O pedido/proposta precisa pertencer ao cliente autenticado.</description></item>
    /// <item><description>A proposta precisa estar aceita e valida.</description></item>
    /// <item><description>Quando ja existe agendamento ativo para a proposta, nova solicitacao e bloqueada.</description></item>
    /// <item><description>A janela enviada deve respeitar disponibilidade e conflitos operacionais.</description></item>
    /// </list>
    ///
    /// O endpoint utiliza as mesmas regras de negocio da agenda do portal do cliente, mas em contrato dedicado mobile.
    /// </remarks>
    /// <param name="orderId">Identificador do pedido.</param>
    /// <param name="proposalId">Identificador da proposta aceita.</param>
    /// <param name="request">Janela selecionada e observacao opcional para o agendamento.</param>
    /// <returns>Resumo atualizado do pedido/proposta e o agendamento criado.</returns>
    /// <response code="200">Agendamento solicitado com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Pedido/proposta nao encontrado para o cliente autenticado.</response>
    /// <response code="409">Conflito de regra de negocio (slot indisponivel, janela duplicada, etc.).</response>
    [HttpPost("{orderId:guid}/proposals/{proposalId:guid}/schedule")]
    [ProducesResponseType(typeof(MobileClientScheduleOrderProposalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScheduleProposal(
        [FromRoute] Guid orderId,
        [FromRoute] Guid proposalId,
        [FromBody] MobileClientOrderProposalScheduleRequestDto request)
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

        var proposalDetails = await _mobileClientOrderService.GetOrderProposalDetailsAsync(clientUserId.Value, orderId, proposalId);
        if (proposalDetails == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_order_proposal_not_found",
                message = "Proposta nao encontrada para o pedido do cliente autenticado."
            });
        }

        if (!proposalDetails.Proposal.Accepted || proposalDetails.Proposal.Invalidated)
        {
            return Conflict(new
            {
                errorCode = "mobile_client_order_proposal_not_accepted",
                message = "Para agendar, a proposta precisa estar aceita e valida."
            });
        }

        if (proposalDetails.CurrentAppointment != null && IsAppointmentBlockingForNewSchedule(proposalDetails.CurrentAppointment.Status))
        {
            return Conflict(new
            {
                errorCode = "mobile_client_order_proposal_already_scheduled",
                message = "Ja existe um agendamento solicitado para esta proposta. Aguarde a confirmacao do prestador."
            });
        }

        var createResult = await _serviceAppointmentService.CreateAsync(
            clientUserId.Value,
            "Client",
            new CreateServiceAppointmentRequestDto(
                orderId,
                proposalDetails.Proposal.ProviderId,
                request.WindowStartUtc,
                request.WindowEndUtc,
                request.Reason));

        if (!createResult.Success || createResult.Appointment == null)
        {
            return MapAppointmentFailure(createResult.ErrorCode, createResult.ErrorMessage);
        }

        var refreshedProposalDetails = await _mobileClientOrderService.GetOrderProposalDetailsAsync(clientUserId.Value, orderId, proposalId);
        var refreshedOrder = await _mobileClientOrderService.GetOrderDetailsAsync(clientUserId.Value, orderId);

        var response = new MobileClientScheduleOrderProposalResponseDto(
            refreshedOrder?.Order ?? proposalDetails.Order,
            refreshedProposalDetails?.Proposal ?? proposalDetails.Proposal,
            MapMobileAppointment(createResult.Appointment, proposalId, proposalDetails.Proposal.ProviderName),
            "Agendamento solicitado com sucesso. Aguarde confirmacao do prestador.");

        return Ok(response);
    }

    private Guid? TryGetClientUserId()
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out var userId) ? userId : null;
    }

    private static MobileClientOrderProposalAppointmentDto MapMobileAppointment(
        ServiceAppointmentDto appointment,
        Guid proposalId,
        string fallbackProviderName)
    {
        var providerName = string.IsNullOrWhiteSpace(fallbackProviderName)
            ? "Prestador"
            : fallbackProviderName;

        return new MobileClientOrderProposalAppointmentDto(
            appointment.Id,
            appointment.ServiceRequestId,
            proposalId,
            appointment.ProviderId,
            providerName,
            appointment.Status,
            ResolveAppointmentStatusLabel(appointment.Status),
            appointment.WindowStartUtc,
            appointment.WindowEndUtc,
            appointment.CreatedAt,
            appointment.UpdatedAt);
    }

    private static IActionResult MapAppointmentFailure(string? errorCode, string? message)
    {
        return errorCode switch
        {
            "forbidden" => new ForbidResult(),
            "provider_not_found" => new NotFoundObjectResult(new { errorCode, message }),
            "request_not_found" => new NotFoundObjectResult(new { errorCode, message }),
            "appointment_not_found" => new NotFoundObjectResult(new { errorCode, message }),
            "appointment_already_exists" => new ConflictObjectResult(new { errorCode, message }),
            "request_window_conflict" => new ConflictObjectResult(new { errorCode, message }),
            "slot_unavailable" => new ConflictObjectResult(new { errorCode, message }),
            "provider_not_assigned" => new ConflictObjectResult(new { errorCode, message }),
            "invalid_state" => new ConflictObjectResult(new { errorCode, message }),
            "policy_violation" => new ConflictObjectResult(new { errorCode, message }),
            "range_too_large" => new BadRequestObjectResult(new { errorCode, message }),
            "invalid_slot_duration" => new BadRequestObjectResult(new { errorCode, message }),
            "invalid_range" => new BadRequestObjectResult(new { errorCode, message }),
            "invalid_window" => new BadRequestObjectResult(new { errorCode, message }),
            "request_closed" => new ConflictObjectResult(new { errorCode, message }),
            _ => new BadRequestObjectResult(new { errorCode, message })
        };
    }

    private static string ResolveAppointmentStatusLabel(string status)
    {
        return status switch
        {
            "PendingProviderConfirmation" => "Aguardando confirmacao do prestador",
            "Confirmed" => "Confirmado",
            "RejectedByProvider" => "Recusado pelo prestador",
            "ExpiredWithoutProviderAction" => "Expirado sem confirmacao",
            "RescheduleRequestedByClient" => "Reagendamento solicitado pelo cliente",
            "RescheduleRequestedByProvider" => "Reagendamento solicitado pelo prestador",
            "RescheduleConfirmed" => "Reagendamento confirmado",
            "CancelledByClient" => "Cancelado pelo cliente",
            "CancelledByProvider" => "Cancelado pelo prestador",
            "Completed" => "Concluido",
            "Arrived" => "Prestador no local",
            "InProgress" => "Servico em andamento",
            _ => "Atualizacao de agendamento"
        };
    }

    private static bool IsAppointmentBlockingForNewSchedule(string status)
    {
        return !status.Equals("CancelledByClient", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("CancelledByProvider", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("RejectedByProvider", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("ExpiredWithoutProviderAction", StringComparison.OrdinalIgnoreCase) &&
               !status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
    }
}
