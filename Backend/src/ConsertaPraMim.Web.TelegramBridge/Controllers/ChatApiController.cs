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
    private readonly ITelegramChatService _telegramChatService;
    private readonly ITelegramChatbotApiClient _telegramChatbotApiClient;

    public ChatApiController(
        ITelegramChatService telegramChatService,
        ITelegramChatbotApiClient telegramChatbotApiClient)
    {
        _telegramChatService = telegramChatService;
        _telegramChatbotApiClient = telegramChatbotApiClient;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<ChatConversationSummaryDto>> GetConversations()
    {
        return Ok(_telegramChatService.GetConversations());
    }

    [HttpGet("{chatId:long}/messages")]
    public ActionResult<IReadOnlyList<ChatMessageDto>> GetMessages(long chatId, [FromQuery] int take = 200)
    {
        return Ok(_telegramChatService.GetMessages(chatId, take));
    }

    [HttpPost("open")]
    public async Task<ActionResult<ChatConversationSummaryDto>> OpenConversation(
        [FromBody] OpenConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.ChatId?.Trim(), out var chatId) || chatId == 0)
        {
            return BadRequest(new { error = "chat_id_invalido" });
        }

        var apiToken = User.FindFirstValue(TelegramBridgeClaimTypes.ApiToken);
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return Unauthorized(new { error = "sessao_sem_token_api" });
        }

        var summary = await _telegramChatService.OpenConversationAsync(chatId, request.Title, cancellationToken);
        await _telegramChatbotApiClient.OpenOrResumeSessionAsync(apiToken, chatId, request.Title, cancellationToken);
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
        try
        {
            var list = files ?? [];
            var message = await _telegramChatService.SendFromPanelAsync(chatId, text, list, cancellationToken);
            var apiToken = User.FindFirstValue(TelegramBridgeClaimTypes.ApiToken);
            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                await _telegramChatbotApiClient.RegisterOutgoingMessageAsync(apiToken, chatId, message, cancellationToken);
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
}
