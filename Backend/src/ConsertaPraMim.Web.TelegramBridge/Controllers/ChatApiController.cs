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

    public ChatApiController(
        ITelegramChatService telegramChatService,
        ITelegramChatbotApiClient telegramChatbotApiClient,
        ITelegramChatbotOrchestrator telegramChatbotOrchestrator)
    {
        _telegramChatService = telegramChatService;
        _telegramChatbotApiClient = telegramChatbotApiClient;
        _telegramChatbotOrchestrator = telegramChatbotOrchestrator;
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
            }

            return Ok(message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
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
}
