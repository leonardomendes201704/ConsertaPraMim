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
    int InvalidatedProposals);

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
    IReadOnlyList<AdminServiceRequestAppointmentDto>? Appointments = null);

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
