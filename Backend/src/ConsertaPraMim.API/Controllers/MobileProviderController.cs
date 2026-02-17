using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do prestador para operacao de pedidos e propostas.
/// </summary>
/// <remarks>
/// O contrato deste controlador e exclusivo para o canal mobile do prestador.
/// Nenhum endpoint aqui substitui contratos usados pelos portais Web, preservando isolamento entre canais.
/// </remarks>
[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/mobile/provider")]
public class MobileProviderController : ControllerBase
{
    private readonly IMobileProviderService _mobileProviderService;

    public MobileProviderController(IMobileProviderService mobileProviderService)
    {
        _mobileProviderService = mobileProviderService;
    }

    /// <summary>
    /// Retorna o dashboard operacional do prestador autenticado.
    /// </summary>
    /// <remarks>
    /// O payload consolida:
    /// <list type="bullet">
    /// <item><description>KPIs de operacao (oportunidades, propostas e agenda);</description></item>
    /// <item><description>lista de pedidos proximos para acao rapida;</description></item>
    /// <item><description>destaques de agenda com foco em pendencias de confirmacao.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="takeNearbyRequests">Quantidade maxima de cards de pedidos proximos no dashboard.</param>
    /// <param name="takeAgenda">Quantidade maxima de cards de agenda no dashboard.</param>
    /// <response code="200">Dashboard retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(MobileProviderDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int takeNearbyRequests = 20,
        [FromQuery] int takeAgenda = 10)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetDashboardAsync(providerUserId, takeNearbyRequests, takeAgenda);
        return Ok(payload);
    }

    /// <summary>
    /// Lista pedidos proximos elegiveis para o prestador autenticado no app.
    /// </summary>
    /// <remarks>
    /// Este endpoint e dedicado ao app do prestador e aplica as regras atuais de matching por raio/categoria/perfil.
    /// </remarks>
    /// <param name="searchTerm">Filtro opcional de descricao/categoria conforme regra atual do matching.</param>
    /// <param name="take">Quantidade maxima de itens retornados.</param>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("requests")]
    [ProducesResponseType(typeof(MobileProviderRequestsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNearbyRequests(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int take = 50)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetNearbyRequestsAsync(providerUserId, searchTerm, take);
        return Ok(payload);
    }

    /// <summary>
    /// Retorna o detalhe de um pedido no contexto do app do prestador.
    /// </summary>
    /// <remarks>
    /// O detalhe inclui:
    /// <list type="bullet">
    /// <item><description>dados do pedido para tomada de decisao comercial;</description></item>
    /// <item><description>eventual proposta ja enviada pelo prestador autenticado;</description></item>
    /// <item><description>flag de elegibilidade para envio de nova proposta.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="requestId">Identificador do pedido.</param>
    /// <response code="200">Detalhe retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Pedido nao encontrado para o prestador autenticado.</response>
    [HttpGet("requests/{requestId:guid}")]
    [ProducesResponseType(typeof(MobileProviderRequestDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestDetails([FromRoute] Guid requestId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetRequestDetailsAsync(providerUserId, requestId);
        if (payload == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_provider_request_not_found",
                message = "Pedido nao encontrado para o prestador autenticado."
            });
        }

        return Ok(payload);
    }

    /// <summary>
    /// Lista propostas do prestador autenticado em contrato dedicado mobile.
    /// </summary>
    /// <param name="take">Quantidade maxima de propostas retornadas.</param>
    /// <response code="200">Propostas retornadas com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("proposals")]
    [ProducesResponseType(typeof(MobileProviderProposalsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyProposals([FromQuery] int take = 100)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetMyProposalsAsync(providerUserId, take);
        return Ok(payload);
    }

    /// <summary>
    /// Retorna agenda operacional do prestador para o app mobile, com pendencias e proximas visitas.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// <list type="bullet">
    /// <item><description>Dados filtrados para o prestador autenticado;</description></item>
    /// <item><description>Separacao em <c>pendingItems</c> (acao do prestador) e <c>upcomingItems</c>;</description></item>
    /// <item><description><c>statusFilter</c> aceita: <c>all</c>, <c>pending</c>, <c>upcoming</c> ou status especifico.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="fromUtc">Data/hora inicial opcional do recorte.</param>
    /// <param name="toUtc">Data/hora final opcional do recorte.</param>
    /// <param name="statusFilter">Filtro opcional por grupo/status.</param>
    /// <param name="take">Quantidade maxima por grupo retornado.</param>
    /// <response code="200">Agenda retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("agenda")]
    [ProducesResponseType(typeof(MobileProviderAgendaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgenda(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] int take = 50)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetAgendaAsync(providerUserId, fromUtc, toUtc, statusFilter, take);
        return Ok(payload);
    }

    /// <summary>
    /// Confirma agendamento pendente de confirmacao no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <response code="200">Agendamento confirmado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para confirmacao.</response>
    [HttpPost("agenda/{appointmentId:guid}/confirm")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAgendaAppointment([FromRoute] Guid appointmentId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.ConfirmAgendaAppointmentAsync(providerUserId, appointmentId);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Recusa agendamento pendente no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Motivo da recusa.</param>
    /// <response code="200">Agendamento recusado com sucesso.</response>
    /// <response code="400">Motivo nao informado ou payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para recusa.</response>
    [HttpPost("agenda/{appointmentId:guid}/reject")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAgendaAppointment(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderRejectAgendaRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.RejectAgendaAppointmentAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Responde solicitacao de reagendamento iniciada pelo cliente.
    /// </summary>
    /// <remarks>
    /// Quando <c>accept=true</c>, o prestador confirma a nova janela proposta.
    /// Quando <c>accept=false</c>, a solicitacao e recusada e o motivo pode ser informado.
    /// </remarks>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Acao de aceite/recusa do reagendamento.</param>
    /// <response code="200">Resposta de reagendamento registrada com sucesso.</response>
    /// <response code="400">Payload invalido para a operacao.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para resposta de reagendamento.</response>
    [HttpPost("agenda/{appointmentId:guid}/reschedule/respond")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RespondAgendaReschedule(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderRespondRescheduleRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.RespondAgendaRescheduleAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Envia proposta comercial para um pedido no app do prestador.
    /// </summary>
    /// <remarks>
    /// Regras de negocio aplicadas:
    /// <list type="bullet">
    /// <item><description>pedido deve estar acessivel/elegivel para o prestador autenticado;</description></item>
    /// <item><description>nao pode existir proposta previa do mesmo prestador para o mesmo pedido;</description></item>
    /// <item><description>valor estimado, quando informado, nao pode ser negativo.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="requestId">Identificador do pedido alvo.</param>
    /// <param name="request">Payload da proposta (valor estimado opcional e mensagem opcional).</param>
    /// <response code="200">Proposta criada com sucesso.</response>
    /// <response code="400">Payload invalido (ex.: valor negativo).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Pedido nao encontrado para o prestador autenticado.</response>
    /// <response code="409">Conflito de regra de negocio (pedido inelegivel/duplicidade de proposta).</response>
    [HttpPost("requests/{requestId:guid}/proposals")]
    [ProducesResponseType(typeof(MobileProviderCreateProposalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProposal(
        [FromRoute] Guid requestId,
        [FromBody] MobileProviderCreateProposalRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.CreateProposalAsync(providerUserId, requestId, request);
        if (result.Success && result.Payload != null)
        {
            return Ok(result.Payload);
        }

        return result.ErrorCode switch
        {
            "mobile_provider_request_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_proposal_already_exists" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_request_not_eligible_for_proposal" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_proposal_invalid_estimated_value" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_proposal_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel enviar a proposta."
            })
        };
    }

    private bool TryGetProviderUserId(out Guid providerUserId)
    {
        providerUserId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out providerUserId);
    }

    private IActionResult MapAgendaFailure(MobileProviderAgendaOperationResultDto result)
    {
        return result.ErrorCode switch
        {
            "mobile_provider_agenda_reject_reason_required" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "appointment_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_state" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_request" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_agenda_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel processar a operacao de agenda."
            })
        };
    }
}
