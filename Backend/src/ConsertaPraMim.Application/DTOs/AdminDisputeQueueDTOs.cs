namespace ConsertaPraMim.Application.DTOs;

public record AdminDisputesQueueResponseDto(
    Guid? HighlightedDisputeCaseId,
    int TotalCount,
    IReadOnlyList<AdminDisputeQueueItemDto> Items);

public record AdminDisputeQueueItemDto(
    Guid DisputeCaseId,
    Guid ServiceRequestId,
    Guid ServiceAppointmentId,
    string Type,
    string Priority,
    string Status,
    string ReasonCode,
    string Description,
    string OpenedByName,
    string OpenedByRole,
    string CounterpartyName,
    string CounterpartyRole,
    DateTime OpenedAtUtc,
    DateTime SlaDueAtUtc,
    DateTime LastInteractionAtUtc,
    bool IsSlaBreached,
    string? City,
    string? Category,
    int AttachmentCount,
    int MessageCount,
    string ActionUrl);
