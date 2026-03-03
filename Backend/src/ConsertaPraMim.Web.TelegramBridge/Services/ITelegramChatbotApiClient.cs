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

    Task<bool> RegisterAssistantMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        TelegramChatbotAssistantReply assistantReply,
        CancellationToken cancellationToken = default);

    Task<TelegramChatbotConversationHistoryDto?> GetConversationHistoryAsync(
        string apiToken,
        Guid conversationId,
        int messageTake,
        int snapshotTake,
        int actionTake,
        CancellationToken cancellationToken = default);

    Task<bool> RegisterContextSnapshotAsync(
        string apiToken,
        Guid conversationId,
        string snapshotType,
        string contextJson,
        string? promptVersion,
        string? modelName,
        int? promptTokens,
        int? completionTokens,
        int? totalTokens,
        string? intentName,
        string? lastStep,
        CancellationToken cancellationToken = default);

    Task<bool> RegisterActionAsync(
        string apiToken,
        Guid conversationId,
        string actionType,
        int status,
        string? intentName,
        string? payloadJson,
        string? resultJson,
        string? errorCode,
        string? errorMessage,
        string? correlationId,
        string? metadataJson,
        string? lastStep,
        int? conversationStatus,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateConversationStateAsync(
        string apiToken,
        Guid conversationId,
        int? status,
        string? lastIntent,
        string? lastStep,
        string? metadataJson,
        CancellationToken cancellationToken = default);

    Task<TelegramCreatedServiceRequestDto?> CreateServiceRequestAsync(
        string apiToken,
        TelegramServiceRequestCreatePayload payload,
        CancellationToken cancellationToken = default);
}
