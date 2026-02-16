using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class PlanGovernanceServiceTests
{
    private readonly Mock<IProviderPlanGovernanceRepository> _governanceRepositoryMock;
    private readonly Mock<IProviderCreditRepository> _providerCreditRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _auditRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly PlanGovernanceService _service;

    public PlanGovernanceServiceTests()
    {
        _governanceRepositoryMock = new Mock<IProviderPlanGovernanceRepository>();
        _providerCreditRepositoryMock = new Mock<IProviderCreditRepository>();
        _auditRepositoryMock = new Mock<IAdminAuditLogRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _userRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<User>());

        _service = new PlanGovernanceService(
            _governanceRepositoryMock.Object,
            _providerCreditRepositoryMock.Object,
            _auditRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task SimulatePriceAsync_ShouldApplyBestPromotionThenCoupon()
    {
        var now = DateTime.UtcNow;
        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingAsync(ProviderPlan.Bronze))
            .ReturnsAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Bronze,
                MonthlyPrice = 100m,
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetPromotionsAsync(false))
            .ReturnsAsync(new List<ProviderPlanPromotion>
            {
                new()
                {
                    Plan = ProviderPlan.Bronze,
                    Name = "Promo 10%",
                    DiscountType = PricingDiscountType.Percentage,
                    DiscountValue = 10m,
                    StartsAtUtc = now.AddDays(-1),
                    EndsAtUtc = now.AddDays(1),
                    IsActive = true
                },
                new()
                {
                    Plan = ProviderPlan.Bronze,
                    Name = "Promo 20 fixo",
                    DiscountType = PricingDiscountType.FixedAmount,
                    DiscountValue = 20m,
                    StartsAtUtc = now.AddDays(-1),
                    EndsAtUtc = now.AddDays(1),
                    IsActive = true
                }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetCouponByCodeAsync("BEMVINDO10"))
            .ReturnsAsync(new ProviderPlanCoupon
            {
                Code = "BEMVINDO10",
                Name = "Bem-vindo",
                Plan = null,
                DiscountType = PricingDiscountType.Percentage,
                DiscountValue = 10m,
                StartsAtUtc = now.AddDays(-1),
                EndsAtUtc = now.AddDays(1),
                IsActive = true
            });

        _governanceRepositoryMock
            .Setup(x => x.GetCouponGlobalUsageCountAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        var result = await _service.SimulatePriceAsync(new AdminPlanPriceSimulationRequestDto(
            ProviderPlan.Bronze,
            "BEMVINDO10",
            now,
            null));

        Assert.True(result.Success);
        Assert.Equal(100m, result.BasePrice);
        Assert.Equal(20m, result.PromotionDiscount);
        Assert.Equal(8m, result.CouponDiscount);
        Assert.Equal(72m, result.FinalPrice);
        Assert.Equal(72m, result.PriceBeforeCredits);
        Assert.Equal(0m, result.AvailableCredits);
        Assert.Equal(0m, result.CreditsApplied);
        Assert.Equal("Promo 20 fixo", result.AppliedPromotion);
        Assert.Equal("BEMVINDO10", result.AppliedCoupon);
    }

    [Fact]
    public async Task SimulatePriceAsync_ShouldApplyAvailableCredits_WhenProviderIsProvided()
    {
        var providerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingAsync(ProviderPlan.Bronze))
            .ReturnsAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Bronze,
                MonthlyPrice = 120m,
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetPromotionsAsync(false))
            .ReturnsAsync(new List<ProviderPlanPromotion>());

        _providerCreditRepositoryMock
            .Setup(x => x.GetWalletAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditWallet
            {
                ProviderId = providerId,
                CurrentBalance = 30m
            });

        var result = await _service.SimulatePriceAsync(new AdminPlanPriceSimulationRequestDto(
            ProviderPlan.Bronze,
            null,
            now,
            providerId));

        Assert.True(result.Success);
        Assert.Equal(120m, result.BasePrice);
        Assert.Equal(120m, result.PriceBeforeCredits);
        Assert.Equal(30m, result.AvailableCredits);
        Assert.Equal(30m, result.CreditsApplied);
        Assert.Equal(90m, result.FinalPrice);
        Assert.Equal(0m, result.CreditsRemaining);
    }

    [Fact]
    public async Task ValidateOperationalSelectionAsync_ShouldRejectWhenRadiusExceedsPlanLimit()
    {
        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingAsync(ProviderPlan.Silver))
            .ReturnsAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Silver,
                MonthlyPrice = 129.90m,
                MaxRadiusKm = 40,
                MaxAllowedCategories = 4,
                AllowedCategories = new List<ServiceCategory>
                {
                    ServiceCategory.Electrical,
                    ServiceCategory.Plumbing,
                    ServiceCategory.Cleaning
                }
            });

        var result = await _service.ValidateOperationalSelectionAsync(
            ProviderPlan.Silver,
            60,
            new List<ServiceCategory> { ServiceCategory.Electrical });

        Assert.False(result.Success);
        Assert.Equal("radius_limit_exceeded", result.ErrorCode);
    }

    [Fact]
    public async Task UpdatePlanSettingAsync_ShouldReturnValidationError_WhenMaxCategoriesExceedsAllowedList()
    {
        var actorUserId = Guid.NewGuid();
        var request = new AdminUpdatePlanSettingRequestDto(
            MonthlyPrice: 99m,
            MaxRadiusKm: 30,
            MaxAllowedCategories: 2,
            AllowedCategories: new List<string> { "Eletrica" });

        var result = await _service.UpdatePlanSettingAsync(
            ProviderPlan.Bronze,
            request,
            actorUserId,
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("validation_error", result.ErrorCode);
        _governanceRepositoryMock.Verify(x => x.AddPlanSettingAsync(It.IsAny<ProviderPlanSetting>()), Times.Never);
        _governanceRepositoryMock.Verify(x => x.UpdatePlanSettingAsync(It.IsAny<ProviderPlanSetting>()), Times.Never);
        _auditRepositoryMock.Verify(x => x.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    [Fact]
    public async Task CreateCouponAsync_ShouldRejectDuplicatedCode()
    {
        var actorUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _governanceRepositoryMock
            .Setup(x => x.GetCouponByCodeAsync("PROMO10"))
            .ReturnsAsync(new ProviderPlanCoupon { Code = "PROMO10", Name = "Existente" });

        var result = await _service.CreateCouponAsync(
            new AdminCreatePlanCouponRequestDto(
                "PROMO10",
                "Cupom repetido",
                ProviderPlan.Gold,
                PricingDiscountType.Percentage,
                10m,
                now.AddDays(-1),
                now.AddDays(1),
                100,
                1),
            actorUserId,
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("duplicate_code", result.ErrorCode);
        _governanceRepositoryMock.Verify(x => x.AddCouponAsync(It.IsAny<ProviderPlanCoupon>()), Times.Never);
    }
}
