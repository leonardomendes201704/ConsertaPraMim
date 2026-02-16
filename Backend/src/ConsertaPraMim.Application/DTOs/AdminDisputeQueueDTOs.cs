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

public record AdminDisputeCaseDetailsDto(
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
    Guid? OwnedByAdminUserId,
    string? OwnedByAdminName,
    DateTime? OwnedAtUtc,
    string? WaitingForRole,
    DateTime OpenedAtUtc,
    DateTime SlaDueAtUtc,
    DateTime LastInteractionAtUtc,
    DateTime? ClosedAtUtc,
    bool IsSlaBreached,
    string? ResolutionSummary,
    string? City,
    string? Category,
    IReadOnlyList<AdminDisputeCaseMessageDto> Messages,
    IReadOnlyList<AdminDisputeCaseAttachmentDto> Attachments,
    IReadOnlyList<AdminDisputeCaseAuditEntryDto> AuditEntries);

public record AdminDisputeCaseMessageDto(
    Guid MessageId,
    string MessageType,
    string MessageText,
    bool IsInternal,
    Guid? AuthorUserId,
    string AuthorRole,
    string? AuthorName,
    DateTime CreatedAtUtc);

public record AdminDisputeCaseAttachmentDto(
    Guid AttachmentId,
    Guid? MessageId,
    string FileUrl,
    string FileName,
    string ContentType,
    string MediaKind,
    long SizeBytes,
    Guid UploadedByUserId,
    string? UploadedByName,
    DateTime CreatedAtUtc);

public record AdminDisputeCaseAuditEntryDto(
    Guid AuditEntryId,
    string EventType,
    string? Message,
    Guid? ActorUserId,
    string ActorRole,
    string? ActorName,
    string? MetadataJson,
    DateTime CreatedAtUtc);

public record AdminUpdateDisputeWorkflowRequestDto(
    string Status,
    string? WaitingForRole = null,
    string? Note = null,
    bool ClaimOwnership = true);

public record AdminDisputeOperationResultDto(
    bool Success,
    AdminDisputeCaseDetailsDto? Case = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record AdminRegisterDisputeDecisionRequestDto(
    string Outcome,
    string Justification,
    string? ResolutionSummary = null);
