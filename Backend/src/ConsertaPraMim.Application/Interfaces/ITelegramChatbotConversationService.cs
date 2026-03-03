using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ITelegramChatbotConversationService
{
    Task<TelegramChatbotConversationDto> OpenOrResumeConversationAsync(TelegramChatbotOpenConversationRequestDto request);
    Task<TelegramChatbotConversationDto?> GetConversationAsync(Guid conversationId, Guid clientId);
    Task<TelegramChatbotConversationHistoryDto?> GetConversationHistoryAsync(
        Guid conversationId,
        Guid clientId,
        int messageTake = 50,
        int snapshotTake = 20,
        int actionTake = 20);
    Task<TelegramChatbotConversationDto?> UpdateConversationStateAsync(TelegramChatbotUpdateConversationStateRequestDto request);
    Task<TelegramChatbotMessageDto?> RegisterMessageAsync(TelegramChatbotRegisterMessageRequestDto request);
    Task<TelegramChatbotContextSnapshotDto?> RegisterContextSnapshotAsync(TelegramChatbotRegisterContextSnapshotRequestDto request);
    Task<TelegramChatbotActionLogDto?> RegisterActionLogAsync(TelegramChatbotRegisterActionLogRequestDto request);
}
