using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatbotOrchestrator
{
    Task<TelegramChatbotAssistantReply?> GenerateAssistantReplyAsync(
        string apiToken,
        long chatId,
        ChatMessageDto clientMessage,
        string conversationTitle,
        CancellationToken cancellationToken = default,
        Guid? authenticatedUserId = null,
        string? authenticatedUserEmail = null,
        string? authenticatedUserRole = null);
}
