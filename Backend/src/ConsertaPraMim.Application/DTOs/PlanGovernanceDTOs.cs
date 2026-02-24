using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record AdminPlanGovernanceSnapshotDto(
    IReadOnlyList<AdminPlanSettingDto> PlanSettings,
    IReadOnlyList<AdminPlanPromotionDto> Promotions,
    IReadOnlyList<AdminPlanCouponDto> Coupons);

public record AdminPlanSettingDto(
    ProviderPlan Plan,
    string PlanLabel,
    decimal MonthlyPrice,
    double MaxRadiusKm,
    int MaxAllowedCategories,
    IReadOnlyList<string> AllowedCategories,
    int ProvidersOutsideOperationalLimits,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminUpdatePlanSettingRequestDto(
    decimal MonthlyPrice,
    double MaxRadiusKm,
    int MaxAllowedCategories,
    IReadOnlyList<string> AllowedCategories);

public record AdminPlanPromotionDto(
    Guid Id,
    ProviderPlan Plan,
    string PlanLabel,
    string Name,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive,
    bool IsCurrentlyActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminCreatePlanPromotionRequestDto(
    ProviderPlan Plan,
    string Name,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

public record AdminUpdatePlanPromotionRequestDto(
    string Name,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

public record AdminUpdatePlanPromotionStatusRequestDto(
    bool IsActive,
    string? Reason);

public record AdminPlanCouponDto(
    Guid Id,
    string Code,
    string Name,
    ProviderPlan? Plan,
    string? PlanLabel,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? MaxGlobalUses,
    int? MaxUsesPerProvider,
    int GlobalUses,
    bool IsActive,
    bool IsCurrentlyActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminCreatePlanCouponRequestDto(
    string Code,
    string Name,
    ProviderPlan? Plan,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? MaxGlobalUses,
    int? MaxUsesPerProvider);

public record AdminUpdatePlanCouponRequestDto(
    string Name,
    ProviderPlan? Plan,
    PricingDiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? MaxGlobalUses,
    int? MaxUsesPerProvider);

public record AdminUpdatePlanCouponStatusRequestDto(
    bool IsActive,
    string? Reason);

public record AdminPlanPriceSimulationRequestDto(
    ProviderPlan Plan,
    string? CouponCode,
    DateTime? AtUtc,
    Guid? ProviderUserId,
    bool ConsumeCredits = false,
    int ExpectedAcceptedProposals = 0,
    int ExpectedScheduledAppointments = 0,
    int ExpectedCompletedServices = 0,
    decimal CreditsChargedPerAcceptedProposal = 0m,
    decimal CreditsChargedPerScheduledAppointment = 0m,
    decimal CreditsChargedPerCompletedService = 0m,
    decimal CreditUnitPrice = 1m);

public record AdminPlanPriceSimulationResultDto(
    bool Success,
    decimal BasePrice,
    decimal PromotionDiscount,
    decimal CouponDiscount,
    decimal FinalPrice,
    string? AppliedPromotion,
    string? AppliedCoupon,
    decimal PriceBeforeCredits = 0m,
    decimal AvailableCredits = 0m,
    decimal CreditsApplied = 0m,
    decimal CreditsRemaining = 0m,
    bool CreditsConsumed = false,
    Guid? CreditsConsumptionEntryId = null,
    int ProjectedResultEvents = 0,
    decimal ProjectedCreditsConsumption = 0m,
    decimal ProjectedVariableRevenue = 0m,
    decimal ProjectedTotalRevenue = 0m,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record AdminRevenueComponentDashboardDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int RangeDays,
    int ActiveProviders,
    decimal FixedMonthlyRecurringRevenue,
    decimal FixedRevenueEstimatedForRange,
    decimal VariableRevenueForRange,
    int VariableRevenueEvents,
    decimal TotalRevenueForRange,
    decimal FixedRevenueSharePercent,
    decimal VariableRevenueSharePercent,
    IReadOnlyList<AdminRevenueComponentPlanBreakdownDto> FixedPlanBreakdown,
    IReadOnlyList<AdminRevenueComponentSeriesPointDto> Series);

public record AdminRevenueComponentPlanBreakdownDto(
    ProviderPlan Plan,
    string PlanLabel,
    int ActiveProviders,
    decimal MonthlyPrice,
    decimal MonthlyRecurringRevenue);

public record AdminRevenueComponentSeriesPointDto(
    DateTime BucketDateUtc,
    decimal FixedRevenueEstimated,
    decimal VariableRevenue,
    int VariableRevenueEvents,
    decimal TotalRevenue);

public record ProviderPlanOfferDto(
    ProviderPlan Plan,
    string PlanLabel,
    decimal BasePrice,
    decimal CurrentPromotionDiscount,
    decimal PriceWithPromotion,
    DateTime? PromotionEndsAtUtc);

public record ProviderOperationalPlanRulesDto(
    ProviderPlan Plan,
    double MaxRadiusKm,
    int MaxAllowedCategories,
    IReadOnlyList<ServiceCategory> AllowedCategories);

public record ProviderOperationalValidationResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
