namespace ConsertaPraMim.Application.DTOs;

public record AdminGrowthFunnelQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    int ProposalSlaMinutes = 30,
    int AcceptanceSlaHours = 24);

public record AdminGrowthFunnelStageDto(
    string Stage,
    int Applicable,
    int Completed,
    int Pending,
    int WithinSla,
    int BreachedSla,
    decimal WithinSlaRatePercent,
    decimal? AverageDurationMinutes,
    decimal? P50DurationMinutes);

public record AdminGrowthAlertDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    decimal CurrentValue,
    decimal ThresholdValue,
    string Unit);

public record AdminGrowthFunnelDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? CategoryFilter,
    string? CityFilter,
    int ProposalSlaMinutes,
    int AcceptanceSlaMinutes,
    int RequestsTotal,
    int RequestsWithAnyProposal,
    int RequestsWithoutProposal,
    int AcceptedRequests,
    int ScheduledOrBeyondRequests,
    int CompletedRequests,
    AdminGrowthFunnelStageDto FirstProposalStage,
    AdminGrowthFunnelStageDto ProposalAcceptanceStage,
    IReadOnlyList<AdminGrowthAlertDto> Alerts);
