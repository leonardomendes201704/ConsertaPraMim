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

public record AdminLiquidityScoreQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    int ProposalSlaMinutes = 30,
    int Take = 50);

public record AdminLiquidityScoreItemDto(
    string Region,
    string Category,
    int DemandRequests,
    int RequestsWithProposal,
    int RequestsWithoutProposal,
    int DistinctProviders,
    decimal ProposalCoverageRatePercent,
    decimal FirstProposalSlaRatePercent,
    decimal? MedianFirstProposalMinutes,
    decimal LiquidityScore,
    string LiquidityBand);

public record AdminLiquidityScoreHistoryPointDto(
    DateTime BucketDateUtc,
    int DemandRequests,
    int RequestsWithProposal,
    int DistinctProviders,
    decimal ProposalCoverageRatePercent,
    decimal FirstProposalSlaRatePercent,
    decimal LiquidityScore);

public record AdminLiquidityScoreResponseDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? CategoryFilter,
    string? CityFilter,
    int ProposalSlaMinutes,
    string FormulaDescription,
    IReadOnlyList<AdminLiquidityScoreItemDto> Items,
    IReadOnlyList<AdminLiquidityScoreHistoryPointDto> History,
    IReadOnlyList<AdminGrowthAlertDto> Alerts);
