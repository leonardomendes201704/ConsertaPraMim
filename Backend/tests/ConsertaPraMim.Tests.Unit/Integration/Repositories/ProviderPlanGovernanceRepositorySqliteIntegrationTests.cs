using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ProviderPlanGovernanceRepositorySqliteIntegrationTests
{
    /// <summary>
    /// Cenario: governanca comercial precisa persistir configuracao de plano com limites e categorias permitidas.
    /// Passos: salva setting do plano Bronze e consulta tanto por plano especifico quanto listagem completa.
    /// Resultado esperado: configuracao e recuperada com os mesmos valores de preco, raio, limites e categorias.
    /// </summary>
    [Fact(DisplayName = "Prestador plan governance repository sqlite integracao | Plan settings crud | Deve persistir e lido back")]
    public async Task PlanSettingsCrud_ShouldPersistAndReadBack()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        await using var dbContext = context;
        using var sqliteConnection = connection;
        var repository = new ProviderPlanGovernanceRepository(dbContext);

        var setting = new ProviderPlanSetting
        {
            Plan = ProviderPlan.Bronze,
            MonthlyPrice = 89.90m,
            MaxRadiusKm = 30,
            MaxAllowedCategories = 2,
            AllowedCategories = new List<ServiceCategory>
            {
                ServiceCategory.Electrical,
                ServiceCategory.Plumbing
            }
        };

        await repository.AddPlanSettingAsync(setting);

        var fromByPlan = await repository.GetPlanSettingAsync(ProviderPlan.Bronze);
        var fromAll = await repository.GetPlanSettingsAsync();

        Assert.NotNull(fromByPlan);
        Assert.Equal(89.90m, fromByPlan!.MonthlyPrice);
        Assert.Equal(30, fromByPlan.MaxRadiusKm);
        Assert.Equal(2, fromByPlan.MaxAllowedCategories);
        Assert.Contains(ServiceCategory.Electrical, fromByPlan.AllowedCategories);
        Assert.Single(fromAll);
    }

    /// <summary>
    /// Cenario: contabilizacao de cupons deve respeitar total global e consumo por prestador.
    /// Passos: cria cupom ativo e registra tres redencoes (duas para A e uma para B), consultando contadores depois.
    /// Resultado esperado: total global e uso por prestador refletem exatamente a distribuicao de redencoes persistidas.
    /// </summary>
    [Fact(DisplayName = "Prestador plan governance repository sqlite integracao | Coupons e redemptions | Deve track usage counters")]
    public async Task CouponsAndRedemptions_ShouldTrackUsageCounters()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        await using var dbContext = context;
        using var sqliteConnection = connection;
        var repository = new ProviderPlanGovernanceRepository(dbContext);
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();

        var coupon = new ProviderPlanCoupon
        {
            Code = "DESC20",
            Name = "Desconto 20",
            Plan = ProviderPlan.Silver,
            DiscountType = PricingDiscountType.Percentage,
            DiscountValue = 20m,
            StartsAtUtc = DateTime.UtcNow.AddDays(-1),
            EndsAtUtc = DateTime.UtcNow.AddDays(30),
            MaxGlobalUses = 100,
            MaxUsesPerProvider = 2,
            IsActive = true
        };

        await repository.AddCouponAsync(coupon);

        await repository.AddCouponRedemptionAsync(new ProviderPlanCouponRedemption
        {
            CouponId = coupon.Id,
            ProviderId = providerA,
            Plan = ProviderPlan.Silver,
            DiscountApplied = 10m,
            AppliedAtUtc = DateTime.UtcNow
        });

        await repository.AddCouponRedemptionAsync(new ProviderPlanCouponRedemption
        {
            CouponId = coupon.Id,
            ProviderId = providerA,
            Plan = ProviderPlan.Silver,
            DiscountApplied = 10m,
            AppliedAtUtc = DateTime.UtcNow
        });

        await repository.AddCouponRedemptionAsync(new ProviderPlanCouponRedemption
        {
            CouponId = coupon.Id,
            ProviderId = providerB,
            Plan = ProviderPlan.Silver,
            DiscountApplied = 10m,
            AppliedAtUtc = DateTime.UtcNow
        });

        var globalUsage = await repository.GetCouponGlobalUsageCountAsync(coupon.Id);
        var usageProviderA = await repository.GetCouponUsageCountByProviderAsync(coupon.Id, providerA);
        var usageProviderB = await repository.GetCouponUsageCountByProviderAsync(coupon.Id, providerB);

        Assert.Equal(3, globalUsage);
        Assert.Equal(2, usageProviderA);
        Assert.Equal(1, usageProviderB);
    }
}
