using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ITelegramChatbotSchedulingService
{
    Task<TelegramChatbotEligibleProvidersResultDto> GetEligibleProvidersAsync(
        Guid clientId,
        Guid serviceRequestId,
        int take = 5);

    Task<TelegramChatbotBatchScheduleResultDto> ScheduleVisitsAsync(
        TelegramChatbotBatchScheduleRequestDto request);
}
