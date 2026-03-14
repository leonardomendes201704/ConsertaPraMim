using System.Security.Claims;
using ConsertaPraMim.API.Contracts;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints do chatbot Telegram autenticado para clientes e prestadores.
/// </summary>
/// <remarks>
/// Este controlador centraliza o contrato da conversa natural mediada por IA:
/// abertura da sessao, registro de mensagens/eventos/contexto e consulta de historico.
/// Os fluxos de sessao e trilha conversacional aceitam `Client` e `Provider`;
/// consultas operacionais de pedidos/agenda continuam restritas a `Client`.
/// </remarks>
[Authorize(Roles = "Client,Provider")]
[ApiController]
[Route("api/telegram-chatbot")]
public class TelegramChatbotController : ControllerBase
{
    private const string ClientRole = "Client";

    private readonly ITelegramChatbotConversationService _telegramChatbotConversationService;
    private readonly ITelegramChatbotSchedulingService _telegramChatbotSchedulingService;

    public TelegramChatbotController(
        ITelegramChatbotConversationService telegramChatbotConversationService,
        ITelegramChatbotSchedulingService? telegramChatbotSchedulingService = null)
    {
        _telegramChatbotConversationService = telegramChatbotConversationService;
        _telegramChatbotSchedulingService = telegramChatbotSchedulingService ?? NullTelegramChatbotSchedulingService.Instance;
    }

    /// <summary>
    /// Abre ou retoma a sessao conversacional do usuario autenticado no canal Telegram.
    /// </summary>
    /// <param name="request">Dados de sessao e contexto inicial da conversa.</param>
    /// <response code="200">Sessao criada ou retomada com sucesso.</response>
    /// <response code="400">Payload invalido ou regra de negocio nao atendida.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    [HttpPost("session")]
    [ProducesResponseType(typeof(TelegramChatbotConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenSession([FromBody] TelegramChatbotOpenSessionRequest request)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        try
        {
            var session = await _telegramChatbotConversationService.OpenOrResumeConversationAsync(
                new TelegramChatbotOpenConversationRequestDto(
                    ClientId: authenticatedUserId,
                    Channel: request.Channel,
                    ChannelConversationId: request.ChannelConversationId,
                    Status: ChatbotConversationStatus.Active,
                    LastIntent: request.LastIntent,
                    LastStep: request.LastStep,
                    MetadataJson: request.MetadataJson,
                    InteractionAtUtc: request.InteractionAtUtc));

            return Ok(session);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_session_request",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Lista prestadores elegiveis para um pedido no fluxo do chatbot Telegram.
    /// </summary>
    /// <param name="serviceRequestId">Identificador do pedido do cliente autenticado.</param>
    /// <param name="take">Quantidade maxima de prestadores sugeridos (1 a 10).</param>
    /// <response code="200">Matching processado com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Cliente sem acesso ao pedido informado.</response>
    /// <response code="404">Pedido nao encontrado.</response>
    [HttpGet("service-requests/{serviceRequestId:guid}/eligible-providers")]
    [ProducesResponseType(typeof(TelegramChatbotEligibleProvidersResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibleProviders(
        [FromRoute] Guid serviceRequestId,
        [FromQuery] int take = 5)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var result = await _telegramChatbotSchedulingService.GetEligibleProvidersAsync(clientId, serviceRequestId, take);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "forbidden" => Forbid(),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    /// <summary>
    /// Solicita agendamento em lote (ate 3 visitas) para prestadores sugeridos no fluxo do chatbot.
    /// </summary>
    /// <param name="serviceRequestId">Identificador do pedido do cliente autenticado.</param>
    /// <param name="request">Lista de visitas com prestador e janela desejada.</param>
    /// <response code="200">Agendamento em lote processado (sucesso total ou parcial).</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Cliente sem acesso ao pedido informado.</response>
    /// <response code="404">Pedido nao encontrado.</response>
    [HttpPost("service-requests/{serviceRequestId:guid}/schedule-visits-batch")]
    [ProducesResponseType(typeof(TelegramChatbotBatchScheduleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleVisitsBatch(
        [FromRoute] Guid serviceRequestId,
        [FromBody] TelegramChatbotBatchScheduleVisitsRequest request)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var batchRequest = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: clientId,
            ServiceRequestId: serviceRequestId,
            Visits: request.Visits
                .Select(visit => new TelegramChatbotBatchScheduleVisitRequestDto(
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: visit.WindowStartUtc,
                    WindowEndUtc: visit.WindowEndUtc,
                    Reason: visit.Reason))
                .ToList());

        var result = await _telegramChatbotSchedulingService.ScheduleVisitsAsync(batchRequest);

        return result.ErrorCode switch
        {
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "forbidden" => Forbid(),
            _ => Ok(result)
        };
    }

    /// <summary>
    /// Lista pedidos do cliente autenticado para consultas naturais no chatbot.
    /// </summary>
    /// <param name="take">Quantidade maxima por pagina (1 a 20).</param>
    /// <param name="skip">Quantidade de registros para paginacao.</param>
    /// <response code="200">Lista de pedidos retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpGet("service-requests")]
    [ProducesResponseType(typeof(TelegramChatbotOrdersResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientOrders(
        [FromQuery] int take = 5,
        [FromQuery] int skip = 0)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var result = await _telegramChatbotSchedulingService.GetClientOrdersAsync(clientId, skip, take);
        return result.Success
            ? Ok(result)
            : BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage });
    }

    /// <summary>
    /// Consulta status consolidado de um pedido especifico do cliente autenticado.
    /// </summary>
    /// <param name="serviceRequestId">Identificador do pedido.</param>
    /// <response code="200">Status retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Cliente sem acesso ao pedido informado.</response>
    /// <response code="404">Pedido nao encontrado.</response>
    [HttpGet("service-requests/{serviceRequestId:guid}/status")]
    [ProducesResponseType(typeof(TelegramChatbotOrderStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus([FromRoute] Guid serviceRequestId)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var result = await _telegramChatbotSchedulingService.GetOrderStatusAsync(clientId, serviceRequestId);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "forbidden" => Forbid(),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    /// <summary>
    /// Consulta detalhes completos de um pedido especifico do cliente autenticado.
    /// </summary>
    /// <param name="serviceRequestId">Identificador do pedido.</param>
    /// <response code="200">Detalhes retornados com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Cliente sem acesso ao pedido informado.</response>
    /// <response code="404">Pedido nao encontrado.</response>
    [HttpGet("service-requests/{serviceRequestId:guid}/details")]
    [ProducesResponseType(typeof(TelegramChatbotOrderDetailsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderDetails([FromRoute] Guid serviceRequestId)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var result = await _telegramChatbotSchedulingService.GetOrderDetailsAsync(clientId, serviceRequestId);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "forbidden" => Forbid(),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    /// <summary>
    /// Lista agendamentos do cliente autenticado para consultas de agenda em linguagem natural.
    /// </summary>
    /// <param name="fromUtc">Data/hora inicial opcional (UTC).</param>
    /// <param name="toUtc">Data/hora final opcional (UTC).</param>
    /// <param name="take">Quantidade maxima por pagina (1 a 20).</param>
    /// <param name="skip">Quantidade de registros para paginacao.</param>
    /// <response code="200">Lista de agendamentos retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpGet("appointments")]
    [ProducesResponseType(typeof(TelegramChatbotAppointmentsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClientAppointments(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int take = 5,
        [FromQuery] int skip = 0)
    {
        if (!TryResolveAuthenticatedClient(out var clientId, out var invalidClientResult))
        {
            return invalidClientResult!;
        }

        var result = await _telegramChatbotSchedulingService.GetClientAppointmentsAsync(
            clientId,
            fromUtc,
            toUtc,
            skip,
            take);

        return result.Success
            ? Ok(result)
            : BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage });
    }

    /// <summary>
    /// Registra uma mensagem da conversa (entrada, saida ou sistema).
    /// </summary>
    /// <param name="request">Payload da mensagem da conversa.</param>
    /// <response code="200">Mensagem persistida com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    /// <response code="404">Conversa nao encontrada para o usuario autenticado.</response>
    [HttpPost("messages")]
    [ProducesResponseType(typeof(TelegramChatbotMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterMessage([FromBody] TelegramChatbotRegisterMessageRequest request)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        if (!Enum.IsDefined(typeof(ChatbotMessageDirection), request.Direction))
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_message_direction",
                message = "Direcao de mensagem invalida para o chatbot."
            });
        }

        try
        {
            var message = await _telegramChatbotConversationService.RegisterMessageAsync(
                new TelegramChatbotRegisterMessageRequestDto(
                    ConversationId: request.ConversationId,
                    ClientId: authenticatedUserId,
                    Direction: request.Direction,
                    Source: request.Source,
                    ChannelMessageId: request.ChannelMessageId,
                    Content: request.Content,
                    IntentName: request.IntentName,
                    ModelName: request.ModelName,
                    PromptTokens: request.PromptTokens,
                    CompletionTokens: request.CompletionTokens,
                    TotalTokens: request.TotalTokens,
                    SentAtUtc: request.SentAtUtc,
                    MetadataJson: request.MetadataJson,
                    LastStep: request.LastStep));

            if (message == null)
            {
                return NotFound(new
                {
                    errorCode = "telegram_chatbot_conversation_not_found",
                    message = "Conversa nao encontrada para o usuario autenticado."
                });
            }

            return Ok(message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_message_request",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Registra snapshot de contexto usado pela orquestracao da IA na conversa.
    /// </summary>
    /// <param name="request">Snapshot de contexto em JSON e metadados de modelo/tokens.</param>
    /// <response code="200">Snapshot persistido com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    /// <response code="404">Conversa nao encontrada para o usuario autenticado.</response>
    [HttpPost("context-snapshots")]
    [ProducesResponseType(typeof(TelegramChatbotContextSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterContextSnapshot([FromBody] TelegramChatbotRegisterContextSnapshotRequest request)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        try
        {
            var contextSnapshot = await _telegramChatbotConversationService.RegisterContextSnapshotAsync(
                new TelegramChatbotRegisterContextSnapshotRequestDto(
                    ConversationId: request.ConversationId,
                    ClientId: authenticatedUserId,
                    SnapshotType: request.SnapshotType,
                    ContextJson: request.ContextJson,
                    PromptVersion: request.PromptVersion,
                    ModelName: request.ModelName,
                    PromptTokens: request.PromptTokens,
                    CompletionTokens: request.CompletionTokens,
                    TotalTokens: request.TotalTokens,
                    CapturedAtUtc: request.CapturedAtUtc,
                    IntentName: request.IntentName,
                    LastStep: request.LastStep));

            if (contextSnapshot == null)
            {
                return NotFound(new
                {
                    errorCode = "telegram_chatbot_conversation_not_found",
                    message = "Conversa nao encontrada para o usuario autenticado."
                });
            }

            return Ok(contextSnapshot);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_snapshot_request",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Registra evento de negocio da conversa (acao executada, sucesso/falha e correlacao).
    /// </summary>
    /// <param name="request">Payload da acao conversacional.</param>
    /// <response code="200">Acao registrada com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    /// <response code="404">Conversa nao encontrada para o usuario autenticado.</response>
    [HttpPost("actions")]
    [ProducesResponseType(typeof(TelegramChatbotActionLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterAction([FromBody] TelegramChatbotRegisterActionRequest request)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        if (!Enum.IsDefined(typeof(ChatbotActionStatus), request.Status))
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_action_status",
                message = "Status de acao invalido para o chatbot."
            });
        }

        try
        {
            var actionLog = await _telegramChatbotConversationService.RegisterActionLogAsync(
                new TelegramChatbotRegisterActionLogRequestDto(
                    ConversationId: request.ConversationId,
                    ClientId: authenticatedUserId,
                    ActionType: request.ActionType,
                    Status: request.Status,
                    IntentName: request.IntentName,
                    PayloadJson: request.PayloadJson,
                    ResultJson: request.ResultJson,
                    ErrorCode: request.ErrorCode,
                    ErrorMessage: request.ErrorMessage,
                    CorrelationId: request.CorrelationId,
                    OccurredAtUtc: request.OccurredAtUtc,
                    MetadataJson: request.MetadataJson,
                    LastStep: request.LastStep,
                    ConversationStatus: request.ConversationStatus));

            if (actionLog == null)
            {
                return NotFound(new
                {
                    errorCode = "telegram_chatbot_conversation_not_found",
                    message = "Conversa nao encontrada para o usuario autenticado."
                });
            }

            return Ok(actionLog);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_action_request",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Atualiza estado da conversa (intencao, passo, metadata e status).
    /// </summary>
    /// <param name="conversationId">Identificador da conversa do chatbot.</param>
    /// <param name="request">Mudancas de estado conversacional.</param>
    /// <response code="200">Estado da conversa atualizado com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    /// <response code="404">Conversa nao encontrada para o usuario autenticado.</response>
    [HttpPatch("conversations/{conversationId:guid}/state")]
    [ProducesResponseType(typeof(TelegramChatbotConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConversationState(
        [FromRoute] Guid conversationId,
        [FromBody] TelegramChatbotUpdateConversationStateRequest request)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        if (request.Status.HasValue && !Enum.IsDefined(typeof(ChatbotConversationStatus), request.Status.Value))
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_conversation_status",
                message = "Status de conversa invalido para o chatbot."
            });
        }

        try
        {
            var conversation = await _telegramChatbotConversationService.UpdateConversationStateAsync(
                new TelegramChatbotUpdateConversationStateRequestDto(
                    ConversationId: conversationId,
                    ClientId: authenticatedUserId,
                    Status: request.Status,
                    LastIntent: request.LastIntent,
                    LastStep: request.LastStep,
                    MetadataJson: request.MetadataJson,
                    InteractionAtUtc: request.InteractionAtUtc));

            if (conversation == null)
            {
                return NotFound(new
                {
                    errorCode = "telegram_chatbot_conversation_not_found",
                    message = "Conversa nao encontrada para o usuario autenticado."
                });
            }

            return Ok(conversation);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                errorCode = "telegram_chatbot_invalid_state_request",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Retorna historico consolidado de mensagens, snapshots e acoes da conversa.
    /// </summary>
    /// <param name="conversationId">Identificador da conversa do chatbot.</param>
    /// <param name="messageTake">Limite de mensagens mais recentes para retorno.</param>
    /// <param name="snapshotTake">Limite de snapshots de contexto para retorno.</param>
    /// <param name="actionTake">Limite de logs de acao para retorno.</param>
    /// <response code="200">Historico retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role `Client` ou `Provider`.</response>
    /// <response code="404">Conversa nao encontrada para o usuario autenticado.</response>
    [HttpGet("conversations/{conversationId:guid}/history")]
    [ProducesResponseType(typeof(TelegramChatbotConversationHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationHistory(
        [FromRoute] Guid conversationId,
        [FromQuery] int messageTake = 50,
        [FromQuery] int snapshotTake = 20,
        [FromQuery] int actionTake = 20)
    {
        if (!TryResolveAuthenticatedUser(out var authenticatedUserId, out var invalidUserResult))
        {
            return invalidUserResult!;
        }

        var history = await _telegramChatbotConversationService.GetConversationHistoryAsync(
            conversationId,
            authenticatedUserId,
            messageTake,
            snapshotTake,
            actionTake);

        if (history == null)
        {
            return NotFound(new
            {
                errorCode = "telegram_chatbot_conversation_not_found",
                message = "Conversa nao encontrada para o usuario autenticado."
            });
        }

        return Ok(history);
    }

    private bool TryResolveAuthenticatedUser(out Guid userId, out IActionResult? errorResult)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(claimValue, out userId))
        {
            errorResult = null;
            return true;
        }

        userId = default;
        errorResult = Unauthorized(new
        {
            errorCode = "telegram_chatbot_invalid_user_claim",
            message = "Nao foi possivel identificar o usuario autenticado."
        });

        return false;
    }

    private bool TryResolveAuthenticatedClient(out Guid clientId, out IActionResult? errorResult)
    {
        if (!TryResolveAuthenticatedUser(out clientId, out errorResult))
        {
            return false;
        }

        if (User.IsInRole(ClientRole))
        {
            errorResult = null;
            return true;
        }

        clientId = default;
        errorResult = Forbid();
        return false;
    }

    private sealed class NullTelegramChatbotSchedulingService : ITelegramChatbotSchedulingService
    {
        public static readonly NullTelegramChatbotSchedulingService Instance = new();

        public Task<TelegramChatbotEligibleProvidersResultDto> GetEligibleProvidersAsync(
            Guid clientId,
            Guid serviceRequestId,
            int take = 5)
        {
            return Task.FromResult(new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de matching do chatbot indisponivel."));
        }

        public Task<TelegramChatbotBatchScheduleResultDto> ScheduleVisitsAsync(TelegramChatbotBatchScheduleRequestDto request)
        {
            return Task.FromResult(new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de agendamento do chatbot indisponivel."));
        }

        public Task<TelegramChatbotOrdersResultDto> GetClientOrdersAsync(Guid clientId, int skip = 0, int take = 5)
        {
            return Task.FromResult(new TelegramChatbotOrdersResultDto(
                Success: false,
                Orders: [],
                TotalCount: 0,
                Skip: skip,
                Take: take,
                HasMore: false,
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de consulta de pedidos do chatbot indisponivel."));
        }

        public Task<TelegramChatbotOrderStatusResultDto> GetOrderStatusAsync(Guid clientId, Guid serviceRequestId)
        {
            return Task.FromResult(new TelegramChatbotOrderStatusResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Protocol: string.Empty,
                Status: string.Empty,
                ProposalsCount: 0,
                AcceptedProposalsCount: 0,
                AppointmentsCount: 0,
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de consulta de status do chatbot indisponivel."));
        }

        public Task<TelegramChatbotOrderDetailsResultDto> GetOrderDetailsAsync(Guid clientId, Guid serviceRequestId)
        {
            return Task.FromResult(new TelegramChatbotOrderDetailsResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de consulta de detalhes do chatbot indisponivel."));
        }

        public Task<TelegramChatbotAppointmentsResultDto> GetClientAppointmentsAsync(
            Guid clientId,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int skip = 0,
            int take = 5)
        {
            return Task.FromResult(new TelegramChatbotAppointmentsResultDto(
                Success: false,
                Appointments: [],
                TotalCount: 0,
                Skip: skip,
                Take: take,
                HasMore: false,
                ErrorCode: "telegram_chatbot_scheduling_service_unavailable",
                ErrorMessage: "Servico de consulta de agenda do chatbot indisponivel."));
        }
    }
}
