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
    Guid? OwnedByAdminUserId,
    string? OwnedByAdminName,
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
    string? ResolutionSummary = null,
    AdminDisputeFinancialDecisionRequestDto? FinancialDecision = null);

public record AdminDisputeFinancialDecisionRequestDto(
    string Action,
    decimal? Amount = null,
    string? Reason = null);

public record AdminDisputeObservabilityQueryDto(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int TopTake = 10);

public record AdminDisputeObservabilityDashboardDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalDisputesOpened,
    int OpenCases,
    int ClosedCases,
    int ResolvedCases,
    int RejectedCases,
    int SlaBreachedOpenCases,
    decimal DecisionProceedingRatePercent,
    decimal AverageResolutionHours,
    decimal MedianResolutionHours,
    IReadOnlyList<AdminStatusCountDto> CasesByType,
    IReadOnlyList<AdminStatusCountDto> CasesByPriority,
    IReadOnlyList<AdminStatusCountDto> CasesByStatus,
    IReadOnlyList<AdminDisputeAnomalyAlertDto> AnomalyAlerts,
    IReadOnlyList<AdminDisputeReasonKpiDto> TopReasons);

public record AdminDisputeReasonKpiDto(
    string ReasonCode,
    int Total,
    int ProceedingCount,
    decimal ProceedingRatePercent);

public record AdminDisputeAnomalyAlertDto(
    string AlertCode,
    string Severity,
    Guid UserId,
    string UserName,
    string UserRole,
    decimal MetricValue,
    decimal Threshold,
    int WindowDays,
    string Description,
    string RecommendedAction,
    IReadOnlyList<Guid> RecentDisputeCaseIds);
