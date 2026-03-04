namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record TelegramServiceRequestTriageState(
    string? CategoryRaw,
    string? CategoryEnum,
    string? ProblemDescription,
    string? Equipment,
    string? Brand,
    string? Model,
    string? ErrorCode,
    string? ZipCode,
    string? Street,
    string? City,
    string? Availability,
    Guid? ServiceRequestId,
    DateTime? ServiceRequestCreatedAtUtc,
    DateTime LastUpdatedAtUtc,
    string? LastClientMessage);

public sealed record TelegramServiceRequestCreatePayload(
    int CategoryValue,
    string CategoryName,
    string Description,
    string Zip,
    string Street,
    string City,
    double Latitude,
    double Longitude);

public sealed record TelegramServiceRequestTriageDecision(
    bool IsTriageIntent,
    TelegramServiceRequestTriageState State,
    IReadOnlyList<string> MissingFields,
    string? FollowUpMessage,
    TelegramServiceRequestCreatePayload? CreatePayload);

public sealed record TelegramCreatedServiceRequestDto(Guid Id);

public sealed record TelegramChatbotEligibleProviderDto(
    Guid ProviderId,
    string ProviderName,
    double DistanceKm,
    double Rating,
    int ReviewCount,
    double RadiusKm,
    IReadOnlyList<int> Categories);

public sealed record TelegramChatbotEligibleProvidersResultDto(
    bool Success,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotEligibleProviderDto> Providers,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record TelegramChatbotBatchScheduleVisitRequestDto(
    Guid ProviderId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public sealed record TelegramChatbotBatchScheduleVisitResultDto(
    Guid ProviderId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    bool Success,
    Guid? AppointmentId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record TelegramChatbotBatchScheduleResultDto(
    bool Success,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotBatchScheduleVisitResultDto> Results,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record TelegramServiceAppointmentSlotDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public sealed record TelegramChatbotOrderSummaryDto(
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

public sealed record TelegramChatbotOrdersResultDto(
    bool Success,
    IReadOnlyList<TelegramChatbotOrderSummaryDto> Orders,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record TelegramChatbotOrderProposalDto(
    Guid ProposalId,
    Guid ProviderId,
    string ProviderName,
    decimal? EstimatedValue,
    bool Accepted,
    DateTime CreatedAtUtc);

public sealed record TelegramChatbotOrderAppointmentDto(
    Guid AppointmentId,
    Guid ProviderId,
    string ProviderName,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public sealed record TelegramChatbotOrderDetailsDto(
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

public sealed record TelegramChatbotOrderDetailsResultDto(
    bool Success,
    Guid ServiceRequestId,
    TelegramChatbotOrderDetailsDto? Details = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record TelegramChatbotOrderStatusResultDto(
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

public sealed record TelegramChatbotAppointmentSummaryDto(
    Guid AppointmentId,
    Guid ServiceRequestId,
    string Protocol,
    Guid ProviderId,
    string ProviderName,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public sealed record TelegramChatbotAppointmentsResultDto(
    bool Success,
    IReadOnlyList<TelegramChatbotAppointmentSummaryDto> Appointments,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string? ErrorCode = null,
    string? ErrorMessage = null);
