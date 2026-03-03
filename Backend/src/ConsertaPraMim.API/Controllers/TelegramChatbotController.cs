using System.Security.Claims;
using ConsertaPraMim.API.Contracts;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints do chatbot Telegram autenticado para clientes.
/// </summary>
/// <remarks>
/// Este controlador centraliza o contrato da conversa natural mediada por IA:
/// abertura da sessao, registro de mensagens/eventos/contexto e consulta de historico.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/telegram-chatbot")]
public class TelegramChatbotController : ControllerBase
{
    private readonly ITelegramChatbotConversationService _telegramChatbotConversationService;

    public TelegramChatbotController(ITelegramChatbotConversationService telegramChatbotConversationService)
    {
        _telegramChatbotConversationService = telegramChatbotConversationService;
    }

    /// <summary>
    /// Abre ou retoma a sessao conversacional do cliente no canal Telegram.
    /// </summary>
    /// <param name="request">Dados de sessao e contexto inicial da conversa.</param>
    /// <response code="200">Sessao criada ou retomada com sucesso.</response>
    /// <response code="400">Payload invalido ou regra de negocio nao atendida.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpPost("session")]
    [ProducesResponseType(typeof(TelegramChatbotConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenSession([FromBody] TelegramChatbotOpenSessionRequest request)
    {
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var session = await _telegramChatbotConversationService.OpenOrResumeConversationAsync(
                new TelegramChatbotOpenConversationRequestDto(
                    ClientId: clientId.Value,
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
    /// Registra uma mensagem da conversa (entrada, saida ou sistema).
    /// </summary>
    /// <param name="request">Payload da mensagem da conversa.</param>
    /// <response code="200">Mensagem persistida com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Conversa nao encontrada para o cliente autenticado.</response>
    [HttpPost("messages")]
    [ProducesResponseType(typeof(TelegramChatbotMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterMessage([FromBody] TelegramChatbotRegisterMessageRequest request)
    {
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
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
                    ClientId: clientId.Value,
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
                    message = "Conversa nao encontrada para o cliente autenticado."
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
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Conversa nao encontrada para o cliente autenticado.</response>
    [HttpPost("context-snapshots")]
    [ProducesResponseType(typeof(TelegramChatbotContextSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterContextSnapshot([FromBody] TelegramChatbotRegisterContextSnapshotRequest request)
    {
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var contextSnapshot = await _telegramChatbotConversationService.RegisterContextSnapshotAsync(
                new TelegramChatbotRegisterContextSnapshotRequestDto(
                    ConversationId: request.ConversationId,
                    ClientId: clientId.Value,
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
                    message = "Conversa nao encontrada para o cliente autenticado."
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
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Conversa nao encontrada para o cliente autenticado.</response>
    [HttpPost("actions")]
    [ProducesResponseType(typeof(TelegramChatbotActionLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterAction([FromBody] TelegramChatbotRegisterActionRequest request)
    {
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
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
                    ClientId: clientId.Value,
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
                    message = "Conversa nao encontrada para o cliente autenticado."
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
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Conversa nao encontrada para o cliente autenticado.</response>
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
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
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
                    ClientId: clientId.Value,
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
                    message = "Conversa nao encontrada para o cliente autenticado."
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
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">Conversa nao encontrada para o cliente autenticado.</response>
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
        var clientId = TryGetAuthenticatedClientId();
        if (!clientId.HasValue)
        {
            return Unauthorized(new
            {
                errorCode = "telegram_chatbot_invalid_client_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var history = await _telegramChatbotConversationService.GetConversationHistoryAsync(
            conversationId,
            clientId.Value,
            messageTake,
            snapshotTake,
            actionTake);

        if (history == null)
        {
            return NotFound(new
            {
                errorCode = "telegram_chatbot_conversation_not_found",
                message = "Conversa nao encontrada para o cliente autenticado."
            });
        }

        return Ok(history);
    }

    private Guid? TryGetAuthenticatedClientId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out var clientId)
            ? clientId
            : null;
    }
}
