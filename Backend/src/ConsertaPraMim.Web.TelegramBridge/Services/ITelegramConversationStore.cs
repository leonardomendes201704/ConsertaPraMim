using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramConversationStore
{
    IReadOnlyList<ChatConversationSummaryDto> GetConversations();

    IReadOnlyList<ChatMessageDto> GetMessages(long chatId, int take);

    ChatConversationSummaryDto EnsureConversation(long chatId, string? title);

    StoreAppendResult AddMessage(
        long chatId,
        string? title,
        bool isOutgoing,
        string senderDisplayName,
        string? text,
        DateTimeOffset sentAtUtc,
        IReadOnlyList<ChatAttachmentDto> attachments,
        string? messageId = null);
}

public sealed record StoreAppendResult(ChatConversationSummaryDto Summary, ChatMessageDto Message);
