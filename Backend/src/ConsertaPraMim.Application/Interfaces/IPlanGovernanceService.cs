using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPlanGovernanceService
{
    Task<AdminPlanGovernanceSnapshotDto> GetAdminSnapshotAsync(
        bool includeInactivePromotions = true,
        bool includeInactiveCoupons = true);

    Task<AdminOperationResultDto> UpdatePlanSettingAsync(
        ProviderPlan plan,
        AdminUpdatePlanSettingRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> CreatePromotionAsync(
        AdminCreatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> UpdatePromotionAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> UpdatePromotionStatusAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> CreateCouponAsync(
        AdminCreatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> UpdateCouponAsync(
        Guid couponId,
        AdminUpdatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminOperationResultDto> UpdateCouponStatusAsync(
        Guid couponId,
        AdminUpdatePlanCouponStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminPlanPriceSimulationResultDto> SimulatePriceAsync(AdminPlanPriceSimulationRequestDto request);

    Task<IReadOnlyList<ProviderPlanOfferDto>> GetProviderPlanOffersAsync(DateTime? atUtc = null);
    Task<ProviderOperationalPlanRulesDto?> GetOperationalRulesAsync(ProviderPlan plan);
    Task<ProviderOperationalValidationResultDto> ValidateOperationalSelectionAsync(
        ProviderPlan plan,
        double radiusKm,
        IReadOnlyCollection<ServiceCategory> categories);
}
