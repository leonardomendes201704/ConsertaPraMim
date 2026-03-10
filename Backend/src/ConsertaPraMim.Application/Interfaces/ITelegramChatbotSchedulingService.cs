using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ITelegramChatbotSchedulingService
{
    Task<TelegramChatbotEligibleProvidersResultDto> GetEligibleProvidersAsync(
        Guid clientId,
        Guid serviceRequestId,
        int take = 5);

    Task<TelegramChatbotOrdersResultDto> GetClientOrdersAsync(
        Guid clientId,
        int skip = 0,
        int take = 5);

    Task<TelegramChatbotOrderStatusResultDto> GetOrderStatusAsync(
        Guid clientId,
        Guid serviceRequestId);

    Task<TelegramChatbotOrderDetailsResultDto> GetOrderDetailsAsync(
        Guid clientId,
        Guid serviceRequestId);

    Task<TelegramChatbotAppointmentsResultDto> GetClientAppointmentsAsync(
        Guid clientId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 5);

    Task<TelegramChatbotBatchScheduleResultDto> ScheduleVisitsAsync(
        TelegramChatbotBatchScheduleRequestDto request);
}
