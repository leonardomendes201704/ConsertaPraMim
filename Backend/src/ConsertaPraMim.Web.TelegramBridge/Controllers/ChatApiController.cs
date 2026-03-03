using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[ApiController]
[Route("api/chats")]
public sealed class ChatApiController : ControllerBase
{
    private readonly ITelegramChatService _telegramChatService;

    public ChatApiController(ITelegramChatService telegramChatService)
    {
        _telegramChatService = telegramChatService;
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

        var summary = await _telegramChatService.OpenConversationAsync(chatId, request.Title, cancellationToken);
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
