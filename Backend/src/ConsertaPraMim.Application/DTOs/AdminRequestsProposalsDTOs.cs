using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record AdminServiceRequestsQueryDto(
    string? SearchTerm,
    string? Status,
    string? Category,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);

public record AdminServiceRequestListItemDto(
    Guid Id,
    string Description,
    string Status,
    string Category,
    string ClientName,
    string ClientEmail,
    string Zip,
    DateTime CreatedAt,
    int TotalProposals,
    int AcceptedProposals,
    int InvalidatedProposals,
    string PaymentStatus = "NoPayments",
    int PaymentTransactions = 0,
    decimal PaidAmount = 0m);

public record AdminServiceRequestsListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminServiceRequestListItemDto> Items);

public record AdminServiceRequestDetailProposalDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string ProviderEmail,
    decimal? EstimatedValue,
    bool Accepted,
    bool IsInvalidated,
    string? InvalidationReason,
    DateTime CreatedAt);

public record AdminServiceRequestAppointmentHistoryDto(
    DateTime OccurredAtUtc,
    string ActorRole,
    string NewStatus,
    string? NewOperationalStatus,
    string? Reason);

public record AdminServiceRequestAppointmentDto(
    Guid AppointmentId,
    Guid ProviderId,
    string ProviderName,
    string Status,
    string? OperationalStatus,
    string? OperationalStatusReason,
    DateTime? OperationalStatusUpdatedAtUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    DateTime? ArrivedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<AdminServiceRequestAppointmentHistoryDto> History);

public record AdminServiceRequestEvidenceDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    Guid? ServiceAppointmentId,
    string? EvidencePhase,
    string FileUrl,
    string? ThumbnailUrl,
    string? PreviewUrl,
    string FileName,
    string ContentType,
    string MediaKind,
    string? Category,
    string? Caption,
    DateTime CreatedAt);

public record AdminServiceRequestScopeChangeAttachmentDto(
    Guid Id,
    string FileUrl,
    string FileName,
    string ContentType,
    string MediaKind,
    long SizeBytes,
    DateTime CreatedAt);

public record AdminServiceRequestScopeChangeDto(
    Guid Id,
    Guid ServiceAppointmentId,
    Guid ProviderId,
    string ProviderName,
    int Version,
    string Status,
    string Reason,
    string AdditionalScopeDescription,
    decimal IncrementalValue,
    decimal PreviousValue,
    decimal NewValue,
    DateTime RequestedAtUtc,
    DateTime? ClientRespondedAtUtc,
    string? ClientResponseReason,
    IReadOnlyList<AdminServiceRequestScopeChangeAttachmentDto> Attachments);

public record AdminServiceRequestDetailsDto(
    Guid Id,
    string Description,
    string Status,
    string Category,
    string Street,
    string City,
    string Zip,
    double Latitude,
    double Longitude,
    string ClientName,
    string ClientEmail,
    string ClientPhone,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<AdminServiceRequestDetailProposalDto> Proposals,
    IReadOnlyList<AdminServiceRequestAppointmentDto>? Appointments = null,
    IReadOnlyList<AdminServiceRequestEvidenceDto>? Evidences = null,
    IReadOnlyList<AdminServiceRequestScopeChangeDto>? ScopeChanges = null,
    int CommercialVersion = 0,
    string? CommercialState = null,
    decimal? CommercialBaseValue = null,
    decimal? CommercialCurrentValue = null,
    DateTime? CommercialUpdatedAtUtc = null,
    string PaymentStatus = "NoPayments",
    int PaymentTransactionsCount = 0,
    decimal PaidAmount = 0m,
    decimal RefundedAmount = 0m,
    DateTime? LastPaymentProcessedAtUtc = null,
    string? LastPaymentMethod = null);

public record AdminUpdateServiceRequestStatusRequestDto(
    string Status,
    string? Reason);

public record AdminProposalsQueryDto(
    Guid? RequestId,
    Guid? ProviderId,
    string? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);

public record AdminProposalListItemDto(
    Guid Id,
    Guid RequestId,
    Guid ProviderId,
    string ProviderName,
    string ProviderEmail,
    decimal? EstimatedValue,
    bool Accepted,
    bool IsInvalidated,
    string? InvalidationReason,
    DateTime CreatedAt);

public record AdminProposalsListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminProposalListItemDto> Items);

public record AdminInvalidateProposalRequestDto(
    string? Reason);

public record AdminOperationResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
