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

public record TelegramChatbotOrderSummaryDto(
    Guid ServiceRequestId,
    string Protocol,
    string Status,
    string Category,
    string Description,
    string City,
    DateTime CreatedAtUtc,
    int ProposalsCount,
    int AcceptedProposalsCount,
    int AppointmentsCount,
    DateTime? NextAppointmentStartUtc = null,
    DateTime? NextAppointmentEndUtc = null,
    string? NextAppointmentStatus = null);

public record TelegramChatbotOrdersResultDto(
    bool Success,
    IReadOnlyList<TelegramChatbotOrderSummaryDto> Orders,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TelegramChatbotOrderProposalDto(
    Guid ProposalId,
    Guid ProviderId,
    string ProviderName,
    decimal? EstimatedValue,
    bool Accepted,
    DateTime CreatedAtUtc);

public record TelegramChatbotOrderAppointmentDto(
    Guid AppointmentId,
    Guid ProviderId,
    string ProviderName,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public record TelegramChatbotOrderDetailsDto(
    Guid ServiceRequestId,
    string Protocol,
    string Status,
    string Category,
    string Description,
    string Street,
    string City,
    string Zip,
    DateTime CreatedAtUtc,
    IReadOnlyList<TelegramChatbotOrderProposalDto> Proposals,
    IReadOnlyList<TelegramChatbotOrderAppointmentDto> Appointments);

public record TelegramChatbotOrderDetailsResultDto(
    bool Success,
    Guid ServiceRequestId,
    TelegramChatbotOrderDetailsDto? Details = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TelegramChatbotOrderStatusResultDto(
    bool Success,
    Guid ServiceRequestId,
    string Protocol,
    string Status,
    int ProposalsCount,
    int AcceptedProposalsCount,
    int AppointmentsCount,
    TelegramChatbotOrderAppointmentDto? NextAppointment = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record TelegramChatbotAppointmentSummaryDto(
    Guid AppointmentId,
    Guid ServiceRequestId,
    string Protocol,
    Guid ProviderId,
    string ProviderName,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record TelegramChatbotAppointmentsResultDto(
    bool Success,
    IReadOnlyList<TelegramChatbotAppointmentSummaryDto> Appointments,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string? ErrorCode = null,
    string? ErrorMessage = null);
