using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatbotApiClient
{
    Task<Guid?> OpenOrResumeSessionAsync(
        string apiToken,
        long chatId,
        string? title,
        CancellationToken cancellationToken = default);

    Task<bool> RegisterOutgoingMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default);

    Task<bool> RegisterIncomingMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default);
}
