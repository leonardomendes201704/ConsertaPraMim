using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Security;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[Authorize]
[ApiController]
[Route("api/chats")]
public sealed class ChatApiController : ControllerBase
{
    private const string MissingApiTokenError = "sessao_sem_token_api";
    private const string InvalidClientSessionError = "sessao_sem_client_id";

    private readonly ITelegramChatService _telegramChatService;
    private readonly ITelegramChatbotApiClient _telegramChatbotApiClient;
    private readonly ITelegramChatbotOrchestrator _telegramChatbotOrchestrator;
    private readonly ITelegramChatbotObservabilityService _observabilityService;

    public ChatApiController(
        ITelegramChatService telegramChatService,
        ITelegramChatbotApiClient telegramChatbotApiClient,
        ITelegramChatbotOrchestrator telegramChatbotOrchestrator,
        ITelegramChatbotObservabilityService? observabilityService = null)
    {
        _telegramChatService = telegramChatService;
        _telegramChatbotApiClient = telegramChatbotApiClient;
        _telegramChatbotOrchestrator = telegramChatbotOrchestrator;
        _observabilityService = observabilityService ?? NullTelegramChatbotObservabilityService.Instance;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatConversationSummaryDto>>> GetConversations(CancellationToken cancellationToken)
    {
        if (!TryResolveClientConversation(out var clientChatId, out var title, out var invalidSessionResult))
        {
            return invalidSessionResult!;
        }

        if (!TryResolveApiToken(out var apiToken, out var missingTokenResult))
        {
            return missingTokenResult!;
        }

        var summary = await EnsureClientConversationAsync(clientChatId, title, apiToken!, cancellationToken);
        return Ok(new[] { summary });
    }

    [HttpGet("{chatId:long}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(
        long chatId,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveClientConversation(out var clientChatId, out var title, out var invalidSessionResult))
        {
            return invalidSessionResult!;
        }

        if (chatId != clientChatId)
        {
            return Forbid();
        }

        if (!TryResolveApiToken(out var apiToken, out var missingTokenResult))
        {
            return missingTokenResult!;
        }

        await EnsureClientConversationAsync(clientChatId, title, apiToken!, cancellationToken);
        return Ok(_telegramChatService.GetMessages(clientChatId, take));
    }

    [HttpPost("open")]
    public async Task<ActionResult<ChatConversationSummaryDto>> OpenConversation(CancellationToken cancellationToken)
    {
        if (!TryResolveClientConversation(out var clientChatId, out var title, out var invalidSessionResult))
        {
            return invalidSessionResult!;
        }

        if (!TryResolveApiToken(out var apiToken, out var missingTokenResult))
        {
            return missingTokenResult!;
        }

        var summary = await EnsureClientConversationAsync(clientChatId, title, apiToken!, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{chatId:long}/messages")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        long chatId,
        [FromForm] string? text,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (!TryResolveClientConversation(out var clientChatId, out var title, out var invalidSessionResult))
        {
            return invalidSessionResult!;
        }

        if (chatId != clientChatId)
        {
            return Forbid();
        }

        if (!TryResolveApiToken(out var apiToken, out var missingTokenResult))
        {
            return missingTokenResult!;
        }

        try
        {
            var list = files ?? [];
            _observabilityService.RecordInboundMessage(list.Count);
            await EnsureClientConversationAsync(clientChatId, title, apiToken!, cancellationToken);
            var message = await _telegramChatService.SendFromClientAsync(clientChatId, text, list, cancellationToken);
            await _telegramChatbotApiClient.RegisterIncomingMessageAsync(apiToken!, clientChatId, message, cancellationToken);
            var assistantReply = await _telegramChatbotOrchestrator.GenerateAssistantReplyAsync(
                apiToken!,
                clientChatId,
                message,
                title,
                cancellationToken);

            if (assistantReply is not null && !string.IsNullOrWhiteSpace(assistantReply.MessageText))
            {
                var assistantMessage = await _telegramChatService.AppendAssistantReplyAsync(
                    clientChatId,
                    assistantReply.MessageText,
                    cancellationToken);

                await _telegramChatbotApiClient.RegisterAssistantMessageAsync(
                    apiToken!,
                    clientChatId,
                    assistantMessage,
                    assistantReply,
                    cancellationToken);

                _observabilityService.RecordOutboundMessage();
            }

            return Ok(message);
        }
        catch (InvalidOperationException exception)
        {
            _observabilityService.RecordIncident(
                stage: "chat_api_send_message",
                errorCode: "chat_validation_error",
                correlationId: null,
                message: exception.Message);
            return BadRequest(new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _observabilityService.RecordIncident(
                stage: "chat_api_send_message",
                errorCode: "chat_api_dependency_error",
                correlationId: null,
                message: exception.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = exception.Message });
        }
    }

    private bool TryResolveClientConversation(
        out long chatId,
        out string title,
        out ActionResult? invalidSessionResult)
    {
        if (!TelegramBridgeClientConversation.TryGetClientId(User, out var clientId))
        {
            chatId = 0;
            title = string.Empty;
            invalidSessionResult = Unauthorized(new { error = InvalidClientSessionError });
            return false;
        }

        chatId = TelegramBridgeClientConversation.BuildChatId(clientId);
        title = TelegramBridgeClientConversation.BuildTitle(User);
        invalidSessionResult = null;
        return true;
    }

    private bool TryResolveApiToken(out string? apiToken, out ActionResult? missingTokenResult)
    {
        apiToken = User.FindFirstValue(TelegramBridgeClaimTypes.ApiToken);
        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            missingTokenResult = null;
            return true;
        }

        missingTokenResult = Unauthorized(new { error = MissingApiTokenError });
        return false;
    }

    private async Task<ChatConversationSummaryDto> EnsureClientConversationAsync(
        long chatId,
        string title,
        string apiToken,
        CancellationToken cancellationToken)
    {
        var summary = await _telegramChatService.OpenConversationAsync(chatId, title, cancellationToken);
        await _telegramChatbotApiClient.OpenOrResumeSessionAsync(apiToken, chatId, title, cancellationToken);
        return summary;
    }

    private sealed class NullTelegramChatbotObservabilityService : ITelegramChatbotObservabilityService
    {
        public static readonly NullTelegramChatbotObservabilityService Instance = new();

        public void RecordInboundMessage(int attachmentCount)
        {
        }

        public void RecordOutboundMessage()
        {
        }

        public void RecordAiOutcome(TelegramChatbotAssistantReply reply, TelegramAiGatewayResult gatewayResult)
        {
        }

        public void RecordBusinessEvent(string eventName, bool success)
        {
        }

        public void RecordDependency(string dependency, bool success, long latencyMilliseconds, string? errorCode = null)
        {
        }

        public void RecordIncident(string stage, string errorCode, string? correlationId, string? message)
        {
        }

        public TelegramChatbotObservabilitySnapshotDto GetSnapshot()
        {
            return new TelegramChatbotObservabilitySnapshotDto(
                GeneratedAtUtc: DateTime.UtcNow,
                Environment: "unknown",
                Traffic: new TelegramChatbotTrafficMetricsDto(0, 0, 0),
                Ai: new TelegramChatbotAiMetricsDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Business: new TelegramChatbotBusinessMetricsDto(0, 0, 0, 0, 0),
                Dependencies: [],
                TopErrors: [],
                RecentIncidents: []);
        }
    }
}
