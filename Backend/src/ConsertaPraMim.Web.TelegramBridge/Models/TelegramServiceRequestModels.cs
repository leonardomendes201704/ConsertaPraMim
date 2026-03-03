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
    string Category,
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
