namespace ConsertaPraMim.Application.DTOs;

public record MobileClientOrderItemDto(
    Guid Id,
    string Title,
    string Status,
    string Category,
    string Date,
    string Icon,
    string? Description,
    int ProposalCount);

public record MobileClientOrdersResponseDto(
    IReadOnlyList<MobileClientOrderItemDto> OpenOrders,
    IReadOnlyList<MobileClientOrderItemDto> FinalizedOrders,
    int OpenOrdersCount,
    int FinalizedOrdersCount,
    int TotalOrdersCount);

public record MobileClientOrderFlowStepDto(
    int Step,
    string Title,
    bool Completed,
    bool Current);

public record MobileClientOrderTimelineEventDto(
    string EventCode,
    string Title,
    string Description,
    DateTime OccurredAtUtc,
    string? RelatedEntityType = null,
    Guid? RelatedEntityId = null);

public record MobileClientOrderDetailsResponseDto(
    MobileClientOrderItemDto Order,
    IReadOnlyList<MobileClientOrderFlowStepDto> FlowSteps,
    IReadOnlyList<MobileClientOrderTimelineEventDto> Timeline);

public record MobileClientOrderProposalDetailsDto(
    Guid Id,
    Guid OrderId,
    Guid ProviderId,
    string ProviderName,
    decimal? EstimatedValue,
    string? Message,
    bool Accepted,
    bool Invalidated,
    string StatusLabel,
    DateTime SentAtUtc,
    int? EstimatedLeadTimeHours = null,
    int? WarrantyDays = null);

public record MobileClientOrderProposalDetailsResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    MobileClientOrderProposalAppointmentDto? CurrentAppointment = null);

public record MobileClientAcceptProposalResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    string Message);

public record MobileClientOrderProposalSlotDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public record MobileClientOrderProposalSlotsResponseDto(
    Guid OrderId,
    Guid ProposalId,
    Guid ProviderId,
    DateOnly Date,
    IReadOnlyList<MobileClientOrderProposalSlotDto> Slots);

public record MobileClientOrderProposalScheduleRequestDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record MobileClientOrderProposalAppointmentDto(
    Guid Id,
    Guid OrderId,
    Guid ProposalId,
    Guid ProviderId,
    string ProviderName,
    string Status,
    string StatusLabel,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record MobileClientScheduleOrderProposalResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    MobileClientOrderProposalAppointmentDto Appointment,
    string Message);

public static class MobileClientProposalComparisonSortBy
{
    public const string BestScore = "best_score";
    public const string LowestPrice = "lowest_price";
    public const string FastestLeadTime = "fastest_lead_time";
    public const string BestRating = "best_rating";
    public const string HighestWarranty = "highest_warranty";

    public static readonly IReadOnlyList<string> All =
    [
        BestScore,
        LowestPrice,
        FastestLeadTime,
        BestRating,
        HighestWarranty
    ];
}

public record MobileClientProposalComparisonItemDto(
    Guid ProposalId,
    Guid OrderId,
    Guid ProviderId,
    string ProviderName,
    decimal? EstimatedValue,
    int? EstimatedLeadTimeHours,
    int? WarrantyDays,
    double ProviderRating,
    int ProviderReviewCount,
    int ProviderCompletedServices,
    int ResponseTimeMinutes,
    bool Accepted,
    bool Invalidated,
    string StatusLabel,
    DateTime SentAtUtc,
    decimal ComparisonScore);

public record MobileClientProposalComparisonSummaryDto(
    int TotalProposals,
    decimal? LowestPrice,
    decimal? HighestPrice,
    int? FastestLeadTimeHours,
    int? HighestWarrantyDays);

public record MobileClientProposalComparisonResponseDto(
    Guid OrderId,
    string ExperimentGroup,
    string SortBy,
    IReadOnlyList<string> AvailableSortOptions,
    MobileClientProposalComparisonSummaryDto Summary,
    IReadOnlyList<MobileClientProposalComparisonItemDto> Proposals);

public record MobileClientProposalComparisonInteractionRequestDto(
    string EventType,
    string? SortBy = null,
    Guid? ProposalId = null,
    string Source = "mobile_client");

public record MobileClientProposalComparisonAbBucketDto(
    string ExperimentGroup,
    int ComparisonViews,
    int SortChanges,
    int ProposalOpens,
    int AcceptedAfterComparison,
    int DistinctRequestsCompared,
    decimal ConversionRatePercent);

public record MobileClientProposalComparisonAbSummaryDto(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<MobileClientProposalComparisonAbBucketDto> Buckets);
