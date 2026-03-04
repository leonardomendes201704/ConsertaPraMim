using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record TelegramChatbotEligibleProviderDto(
    Guid ProviderId,
    string ProviderName,
    double DistanceKm,
    double Rating,
    int ReviewCount,
    double CoverageRadiusKm,
    string? BaseZipCode,
    IReadOnlyList<ServiceCategory> Categories);

public record TelegramChatbotEligibleProvidersResultDto(
    bool Success,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotEligibleProviderDto> Providers,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TelegramChatbotBatchScheduleVisitRequestDto(
    Guid ProviderId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record TelegramChatbotBatchScheduleRequestDto(
    Guid ClientId,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto> Visits);

public record TelegramChatbotBatchScheduleVisitResultDto(
    Guid ProviderId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    bool Success,
    Guid? AppointmentId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TelegramChatbotBatchScheduleResultDto(
    bool Success,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotBatchScheduleVisitResultDto> Results,
    string? ErrorCode = null,
    string? ErrorMessage = null);
