namespace ConsertaPraMim.Application.DTOs;

public record AdminGrowthFunnelQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    int ProposalSlaMinutes = 30,
    int AcceptanceSlaHours = 24);

public record AdminGrowthExecutiveCockpitQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    int ProposalSlaMinutes = 30,
    int AcceptanceSlaHours = 24,
    int NorthStarResolutionHours = 72);

public record AdminGrowthQuarterTargetDto(
    string QuarterCode,
    decimal TargetPercent,
    decimal CurrentPercent,
    bool IsCurrentQuarter,
    string Status);

public record AdminGrowthKpiCardDto(
    string Code,
    string Label,
    decimal Value,
    string Unit,
    string Description,
    decimal? TargetValue = null);

public record AdminGrowthWeeklyTrendPointDto(
    DateTime WeekStartUtc,
    int RequestsOpened,
    int RequestsWithProposal,
    int RequestsAccepted,
    int RequestsScheduledOrBeyond,
    decimal NorthStarRatePercent);

public record AdminGrowthExecutiveCockpitDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? CategoryFilter,
    string? CityFilter,
    int ProposalSlaMinutes,
    int AcceptanceSlaHours,
    int NorthStarResolutionHours,
    string NorthStarName,
    string NorthStarFormula,
    decimal NorthStarRatePercent,
    int NorthStarNumerator,
    int NorthStarDenominator,
    IReadOnlyList<AdminGrowthQuarterTargetDto> QuarterTargets,
    IReadOnlyList<AdminGrowthKpiCardDto> Kpis,
    IReadOnlyList<AdminGrowthWeeklyTrendPointDto> WeeklyTrend);

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

public record AdminProviderReactivationSegmentsQueryDto(
    DateTime? AsOfUtc,
    int WarmFromDays = 7,
    int ColdFromDays = 15,
    int DormantFromDays = 31,
    int HibernatedFromDays = 61,
    int PreviewTake = 50);

public record AdminProviderReactivationSegmentBreakdownDto(
    string SegmentCode,
    string SegmentLabel,
    int MinDaysInclusive,
    int? MaxDaysInclusive,
    int Providers,
    decimal ProvidersSharePercent,
    int DistinctCategories,
    int DistinctRegions,
    string? TopCategory,
    string? TopRegion);

public record AdminProviderReactivationProviderPreviewDto(
    Guid ProviderId,
    string ProviderName,
    string ProviderEmail,
    int InactivityDays,
    DateTime? LastActivityAtUtc,
    string SegmentCode,
    string SegmentLabel,
    string Category,
    string Region);

public record AdminProviderReactivationSegmentsDto(
    DateTime AsOfUtc,
    int TotalProviders,
    int ActiveProviders,
    int InactiveProviders,
    IReadOnlyList<AdminProviderReactivationSegmentBreakdownDto> Segments,
    IReadOnlyList<AdminProviderReactivationProviderPreviewDto> Preview);

public record AdminProviderReactivationCampaignRunRequestDto(
    DateTime? AsOfUtc,
    int CadenceHours = 24,
    int MaxRecipients = 200,
    bool ForceRun = false,
    string? SegmentCode = null,
    bool RespectOptOut = true,
    int DefaultMaxTouchesPerWeek = 3,
    int FrequencyWindowDays = 7,
    bool SendSystem = true,
    bool SendPush = true,
    bool SendEmail = false,
    string? MessageTemplate = null);

public record AdminProviderReactivationCampaignDeliverySummaryDto(
    bool SystemEnabled,
    bool PushEnabled,
    bool EmailEnabled,
    int SystemSent,
    int PushSent,
    int EmailSent,
    int Failed,
    IReadOnlyList<string> Errors);

public record AdminProviderReactivationPolicySummaryDto(
    bool RespectOptOut,
    int FrequencyWindowDays,
    int DefaultMaxTouchesPerWeek,
    int SuppressedByOptOut,
    int SuppressedByFrequency,
    int EligibleAfterPolicy);

public record AdminProviderReactivationPreferenceUpsertRequestDto(
    Guid ProviderId,
    bool OptOut,
    int MaxTouchesPerWeek = 3,
    string? Reason = null);

public record AdminProviderReactivationPreferenceDto(
    Guid ProviderId,
    bool OptOut,
    int MaxTouchesPerWeek,
    string? Reason,
    DateTime UpdatedAtUtc,
    string UpdatedByEmail);

public record AdminProviderReactivationCampaignRunResultDto(
    Guid CampaignId,
    DateTime RequestedAtUtc,
    bool Executed,
    string Status,
    string Message,
    int CadenceHours,
    bool ForceRun,
    int SelectedProviders,
    string? SegmentCode,
    DateTime? PreviousCampaignAtUtc,
    IReadOnlyList<AdminProviderReactivationProviderPreviewDto> Recipients,
    AdminProviderReactivationCampaignDeliverySummaryDto? Delivery = null,
    AdminProviderReactivationPolicySummaryDto? Policy = null);

public record AdminProviderReactivationCampaignPerformanceQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Take = 50);

public record AdminProviderReactivationCampaignPerformanceItemDto(
    Guid CampaignId,
    DateTime RequestedAtUtc,
    string Status,
    int SelectedProviders,
    int ReactivatedProviders,
    decimal ReactivationRatePercent,
    int SystemSent,
    int PushSent,
    int EmailSent,
    int Failed);

public record AdminProviderReactivationCampaignPerformanceDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalCampaigns,
    int TotalSelectedProviders,
    int TotalReactivatedProviders,
    decimal ReactivationRatePercent,
    int TotalSystemSent,
    int TotalPushSent,
    int TotalEmailSent,
    int TotalFailed,
    IReadOnlyList<AdminProviderReactivationCampaignPerformanceItemDto> Items);

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
