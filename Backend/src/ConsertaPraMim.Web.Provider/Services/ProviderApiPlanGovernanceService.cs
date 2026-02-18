using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiPlanGovernanceService : IPlanGovernanceService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiPlanGovernanceService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public Task<AdminPlanGovernanceSnapshotDto> GetAdminSnapshotAsync(
        bool includeInactivePromotions = true,
        bool includeInactiveCoupons = true) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> UpdatePlanSettingAsync(
        ProviderPlan plan,
        AdminUpdatePlanSettingRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> CreatePromotionAsync(
        AdminCreatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> UpdatePromotionAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> UpdatePromotionStatusAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionStatusRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> CreateCouponAsync(
        AdminCreatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> UpdateCouponAsync(
        Guid couponId,
        AdminUpdatePlanCouponRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public Task<AdminOperationResultDto> UpdateCouponStatusAsync(
        Guid couponId,
        AdminUpdatePlanCouponStatusRequestDto request,
        Guid actorUserId,
        string actorEmail) =>
        throw new NotSupportedException("Operacao nao suportada no portal prestador.");

    public async Task<AdminPlanPriceSimulationResultDto> SimulatePriceAsync(AdminPlanPriceSimulationRequestDto request)
    {
        var response = await _apiCaller.SendAsync<AdminPlanPriceSimulationResultDto>(
            HttpMethod.Post,
            "/api/provider-credits/me/plan-governance/simulate",
            request);

        return response.Payload ?? new AdminPlanPriceSimulationResultDto(
            false,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            ErrorCode: "api_error",
            ErrorMessage: response.ErrorMessage);
    }

    public Task<IReadOnlyList<ProviderPlanOfferDto>> GetProviderPlanOffersAsync(DateTime? atUtc = null) =>
        Task.FromResult<IReadOnlyList<ProviderPlanOfferDto>>([]);

    public Task<ProviderOperationalPlanRulesDto?> GetOperationalRulesAsync(ProviderPlan plan) =>
        Task.FromResult<ProviderOperationalPlanRulesDto?>(null);

    public Task<ProviderOperationalValidationResultDto> ValidateOperationalSelectionAsync(
        ProviderPlan plan,
        double radiusKm,
        IReadOnlyCollection<ServiceCategory> categories) =>
        Task.FromResult(new ProviderOperationalValidationResultDto(false, "not_supported", "Operacao nao suportada no portal prestador."));
}
