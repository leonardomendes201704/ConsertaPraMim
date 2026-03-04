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
