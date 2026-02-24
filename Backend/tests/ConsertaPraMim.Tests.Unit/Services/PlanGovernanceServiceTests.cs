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
    /// Cenario: a simulacao de preco recebe duas promocoes validas e um cupom cumulativo no mesmo periodo.
    /// Passos: o teste configura plano Bronze com promocao percentual e promocao de valor fixo, alem de cupom ativo.
    /// Resultado esperado: o servico escolhe a melhor promocao, aplica o cupom sobre o valor promocional e retorna preco final correto.
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
    /// Cenario: um prestador possui saldo em carteira e solicita simulacao de assinatura sem consumo definitivo.
    /// Passos: o teste informa providerId, carteira com credito disponivel e executa a simulacao do plano.
    /// Resultado esperado: os creditos disponiveis entram no calculo, abatendo o valor final sem gravar consumo.
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
    /// Cenario: a simulacao eh solicitada com opcao explicita de consumir creditos do prestador.
    /// Passos: o teste prepara carteira com saldo, habilita consumeCredits e monitora a gravacao no razao de creditos.
    /// Resultado esperado: o desconto por creditos eh aplicado, o consumo fica marcado e a entrada de ledger eh persistida.
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
    /// Cenario: a carteira possui creditos vencidos que nao podem ser usados na cobranca do plano.
    /// Passos: o teste injeta entrada de grant expirada, executa simulacao e observa rotina automatica de expiracao.
    /// Resultado esperado: os creditos vencidos sao convertidos em evento de expire e o preco final nao recebe abatimento indevido.
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
    /// Cenario: operacao admin precisa visualizar participacao de receita fixa e variavel no recorte.
    /// Passos: o teste monta prestadores ativos por plano e debitos de creditos no periodo.
    /// Resultado esperado: dashboard retorna MRR fixo, receita variavel do ledger, participacao percentual e serie diaria consolidada.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Revenue components dashboard | Deve consolidar assinatura fixa e receita variavel")]
    public async Task GetRevenueComponentDashboardAsync_ShouldAggregateFixedAndVariableRevenue()
    {
        var fromUtc = DateTime.UtcNow.Date.AddDays(-2);
        var toUtc = fromUtc.AddDays(2).AddHours(23);
        var bronzeProviderId = Guid.NewGuid();
        var silverProviderId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = bronzeProviderId,
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Bronze }
                },
                new()
                {
                    Id = silverProviderId,
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Silver }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = false,
                    ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Gold }
                }
            });

        _governanceRepositoryMock
            .Setup(x => x.GetPlanSettingsAsync())
            .ReturnsAsync(new List<ProviderPlanSetting>
            {
                new() { Plan = ProviderPlan.Bronze, MonthlyPrice = 100m, MaxRadiusKm = 25, MaxAllowedCategories = 3, AllowedCategories = new List<ServiceCategory> { ServiceCategory.Electrical } },
                new() { Plan = ProviderPlan.Silver, MonthlyPrice = 200m, MaxRadiusKm = 40, MaxAllowedCategories = 5, AllowedCategories = new List<ServiceCategory> { ServiceCategory.Plumbing } },
                new() { Plan = ProviderPlan.Gold, MonthlyPrice = 300m, MaxRadiusKm = 60, MaxAllowedCategories = 10, AllowedCategories = new List<ServiceCategory> { ServiceCategory.Cleaning } }
            });

        _providerCreditRepositoryMock
            .Setup(x => x.GetEntriesChronologicalAsync(bronzeProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderCreditLedgerEntry>
            {
                new()
                {
                    ProviderId = bronzeProviderId,
                    EntryType = ProviderCreditLedgerEntryType.Debit,
                    RevenueComponent = ProviderCreditRevenueComponent.VariableCredits,
                    Amount = 15m,
                    EffectiveAtUtc = fromUtc.AddHours(10)
                },
                new()
                {
                    ProviderId = bronzeProviderId,
                    EntryType = ProviderCreditLedgerEntryType.Debit,
                    RevenueComponent = ProviderCreditRevenueComponent.VariableCredits,
                    Amount = 5m,
                    EffectiveAtUtc = fromUtc.AddDays(1).AddHours(8)
                }
            });

        _providerCreditRepositoryMock
            .Setup(x => x.GetEntriesChronologicalAsync(silverProviderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderCreditLedgerEntry>
            {
                new()
                {
                    ProviderId = silverProviderId,
                    EntryType = ProviderCreditLedgerEntryType.Debit,
                    RevenueComponent = ProviderCreditRevenueComponent.VariableCredits,
                    Amount = 20m,
                    EffectiveAtUtc = fromUtc.AddDays(1).AddHours(14)
                }
            });

        var result = await _service.GetRevenueComponentDashboardAsync(fromUtc, toUtc);

        Assert.Equal(2, result.ActiveProviders);
        Assert.Equal(300m, result.FixedMonthlyRecurringRevenue);
        Assert.Equal(30m, result.FixedRevenueEstimatedForRange);
        Assert.Equal(40m, result.VariableRevenueForRange);
        Assert.Equal(3, result.VariableRevenueEvents);
        Assert.Equal(70m, result.TotalRevenueForRange);
        Assert.Equal(42.86m, result.FixedRevenueSharePercent);
        Assert.Equal(57.14m, result.VariableRevenueSharePercent);
        Assert.Equal(3, result.RangeDays);
        Assert.Equal(3, result.Series.Count);
        Assert.Equal(40m, result.Series.Sum(x => x.VariableRevenue));
        Assert.Contains(result.FixedPlanBreakdown, x => x.Plan == ProviderPlan.Bronze && x.ActiveProviders == 1 && x.MonthlyRecurringRevenue == 100m);
        Assert.Contains(result.FixedPlanBreakdown, x => x.Plan == ProviderPlan.Silver && x.ActiveProviders == 1 && x.MonthlyRecurringRevenue == 200m);
    }

    /// <summary>
    /// Cenario: operacao/admin precisa avaliar rollout do modelo hibrido por cohorts de confianca e compliance.
    /// Passos: o teste monta base de prestadores com combinacoes de plano/trust/pending compliance.
    /// Resultado esperado: estrategia retorna cohorts priorizados, contadores elegiveis/bloqueados e fases com metas.
    /// </summary>
    [Fact(DisplayName = "Plan governance servico | Hybrid rollout strategy | Deve consolidar cohorts elegiveis e holdout")]
    public async Task GetHybridRolloutStrategyAsync_ShouldBuildCohortStrategy()
    {
        _userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        Plan = ProviderPlan.Gold,
                        TrustStatus = ProviderTrustStatus.Verified,
                        HasOperationalCompliancePending = false,
                        OnboardingStatus = ProviderOnboardingStatus.Active
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        Plan = ProviderPlan.Silver,
                        TrustStatus = ProviderTrustStatus.Verified,
                        HasOperationalCompliancePending = false,
                        OnboardingStatus = ProviderOnboardingStatus.Active
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        Plan = ProviderPlan.Bronze,
                        TrustStatus = ProviderTrustStatus.Pending,
                        RiskLevel = ProviderRiskLevel.Low,
                        HasOperationalCompliancePending = false,
                        OnboardingStatus = ProviderOnboardingStatus.Active
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        Plan = ProviderPlan.Bronze,
                        TrustStatus = ProviderTrustStatus.Restricted,
                        HasOperationalCompliancePending = true,
                        OnboardingStatus = ProviderOnboardingStatus.PendingApproval
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Client,
                    IsActive = true
                }
            });

        var result = await _service.GetHybridRolloutStrategyAsync();

        Assert.Equal(4, result.ActiveProviders);
        Assert.Equal(3, result.EligibleProviders);
        Assert.Equal(1, result.BlockedProviders);
        Assert.Equal(4, result.Milestones.Count);
        Assert.Equal(5, result.Cohorts.Count);
        Assert.Contains(result.Cohorts, x => x.CohortKey == "verified_gold" && x.Providers == 1);
        Assert.Contains(result.Cohorts, x => x.CohortKey == "verified_silver" && x.Providers == 1);
        Assert.Contains(result.Cohorts, x => x.CohortKey == "pending_low_risk" && x.Providers == 1);
        Assert.Contains(result.Cohorts, x => x.CohortKey == "restricted_or_non_compliant" && x.Providers == 1 && x.SuggestedRolloutPercent == 0);
    }

    /// <summary>
    /// Cenario: a configuracao operacional solicitada extrapola o raio maximo permitido pelo plano contratado.
    /// Passos: o teste carrega limites do plano Silver e envia selecao com raio superior ao teto configurado.
    /// Resultado esperado: a validacao falha com codigo de limite de raio excedido.
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
    /// Cenario: o admin tenta salvar regra de plano com inconsistencia entre limite maximo e lista de categorias permitidas.
    /// Passos: o teste envia maximo de categorias maior que a quantidade efetiva informada em AllowedCategories.
    /// Resultado esperado: o servico retorna erro de validacao e bloqueia qualquer persistencia ou auditoria.
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
    /// Cenario: o admin tenta cadastrar cupom com codigo que ja existe na base.
    /// Passos: o teste simula retorno de cupom existente para o mesmo codigo e solicita nova criacao.
    /// Resultado esperado: o servico rejeita com erro de duplicidade e nao grava novo cupom.
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
