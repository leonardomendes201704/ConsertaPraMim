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
        _providerCreditRepositoryMock
            .Setup(x => x.GetEntriesChronologicalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderCreditLedgerEntry>());

        _service = new PlanGovernanceService(
            _governanceRepositoryMock.Object,
            _providerCreditRepositoryMock.Object,
            _auditRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Simulate price | Deve apply best promotion then coupon.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Simulate price | Deve apply best promotion then coupon")]
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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Simulate price | Deve apply available creditos quando prestador provided.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Simulate price | Deve apply available creditos quando prestador provided")]
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
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true
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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Simulate price | Deve consume creditos quando consume creditos verdadeiro.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Simulate price | Deve consume creditos quando consume creditos verdadeiro")]
    public async Task SimulatePriceAsync_ShouldConsumeCredits_WhenConsumeCreditsIsTrue()
    {
        var providerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingAsync(ProviderPlan.Bronze))
            .ReturnsAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Bronze,
                MonthlyPrice = 80m,
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = new List<ServiceCategory> { ServiceCategory.Electrical }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetPromotionsAsync(false))
            .ReturnsAsync(new List<ProviderPlanPromotion>());

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true
            });

        _providerCreditRepositoryMock
            .Setup(x => x.GetWalletAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditWallet
            {
                ProviderId = providerId,
                CurrentBalance = 50m
            });

        _providerCreditRepositoryMock
            .Setup(x => x.AppendEntryAsync(
                providerId,
                It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Func<ProviderCreditWallet, ProviderCreditLedgerEntry> factory, CancellationToken _) =>
            {
                var wallet = new ProviderCreditWallet
                {
                    ProviderId = providerId,
                    CurrentBalance = 50m
                };

                var entry = factory(wallet);
                entry.Id = Guid.NewGuid();
                return entry;
            });

        var result = await _service.SimulatePriceAsync(new AdminPlanPriceSimulationRequestDto(
            ProviderPlan.Bronze,
            null,
            now,
            providerId,
            true));

        Assert.True(result.Success);
        Assert.Equal(80m, result.PriceBeforeCredits);
        Assert.Equal(50m, result.AvailableCredits);
        Assert.Equal(50m, result.CreditsApplied);
        Assert.Equal(30m, result.FinalPrice);
        Assert.True(result.CreditsConsumed);
        Assert.NotNull(result.CreditsConsumptionEntryId);

        _providerCreditRepositoryMock.Verify(x => x.AppendEntryAsync(
            providerId,
            It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Simulate price | Deve expire creditos automatically before applying balance.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Simulate price | Deve expire creditos automatically before applying balance")]
    public async Task SimulatePriceAsync_ShouldExpireCreditsAutomatically_BeforeApplyingBalance()
    {
        var providerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingAsync(ProviderPlan.Bronze))
            .ReturnsAsync(new ProviderPlanSetting
            {
                Plan = ProviderPlan.Bronze,
                MonthlyPrice = 100m,
                MaxRadiusKm = 25,
                MaxAllowedCategories = 3,
                AllowedCategories = new List<ServiceCategory> { ServiceCategory.Electrical }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetPromotionsAsync(false))
            .ReturnsAsync(new List<ProviderPlanPromotion>());

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true
            });

        _providerCreditRepositoryMock
            .Setup(x => x.GetEntriesChronologicalAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderCreditLedgerEntry>
            {
                new()
                {
                    ProviderId = providerId,
                    EntryType = ProviderCreditLedgerEntryType.Grant,
                    Amount = 40m,
                    EffectiveAtUtc = now.AddDays(-10),
                    ExpiresAtUtc = now.AddDays(-1)
                }
            });

        _providerCreditRepositoryMock
            .Setup(x => x.AppendEntryAsync(
                providerId,
                It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Func<ProviderCreditWallet, ProviderCreditLedgerEntry> factory, CancellationToken _) =>
            {
                var wallet = new ProviderCreditWallet
                {
                    ProviderId = providerId,
                    CurrentBalance = 40m
                };

                var entry = factory(wallet);
                entry.Id = Guid.NewGuid();
                Assert.Equal(ProviderCreditLedgerEntryType.Expire, entry.EntryType);
                Assert.Equal(40m, entry.Amount);
                return entry;
            });

        _providerCreditRepositoryMock
            .Setup(x => x.GetWalletAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditWallet
            {
                ProviderId = providerId,
                CurrentBalance = 0m
            });

        var result = await _service.SimulatePriceAsync(new AdminPlanPriceSimulationRequestDto(
            ProviderPlan.Bronze,
            null,
            now,
            providerId));

        Assert.True(result.Success);
        Assert.Equal(100m, result.FinalPrice);
        Assert.Equal(0m, result.AvailableCredits);
        Assert.Equal(0m, result.CreditsApplied);

        _providerCreditRepositoryMock.Verify(x => x.AppendEntryAsync(
            providerId,
            It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Validate operational selection | Deve reject quando radius exceeds plan limit.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Validate operational selection | Deve reject quando radius exceeds plan limit")]
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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Atualizar plan setting | Deve retornar validation erro quando max categories exceeds allowed listar.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Atualizar plan setting | Deve retornar validation erro quando max categories exceeds allowed listar")]
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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Plan governance servico | Criar coupon | Deve reject duplicated code.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Criar coupon | Deve reject duplicated code")]
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
