using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatRealtimeNotifier
{
    Task BroadcastConversationUpsertedAsync(ChatConversationSummaryDto summary, CancellationToken cancellationToken);

    Task BroadcastConversationMessageAsync(ChatConversationSummaryDto summary, ChatMessageDto message, CancellationToken cancellationToken);
}
