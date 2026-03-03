using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.Http;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatService
{
    IReadOnlyList<ChatConversationSummaryDto> GetConversations();

    IReadOnlyList<ChatMessageDto> GetMessages(long chatId, int take);

    Task<ChatConversationSummaryDto> OpenConversationAsync(long chatId, string? title, CancellationToken cancellationToken);

    Task<ChatMessageDto> SendFromClientAsync(long chatId, string? text, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken);

    Task<ChatMessageDto> SendFromPanelAsync(long chatId, string? text, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken);

    Task<ChatMessageDto?> ReceiveFromTelegramAsync(TelegramMessage message, CancellationToken cancellationToken);
}
