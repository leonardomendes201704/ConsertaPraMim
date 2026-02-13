using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Configuration;

public static class ProviderSubscriptionPricingCatalog
{
    private static readonly IReadOnlyDictionary<ProviderPlan, decimal> MonthlyPrices = new Dictionary<ProviderPlan, decimal>
    {
        [ProviderPlan.Trial] = 0m,
        [ProviderPlan.Bronze] = 79.90m,
        [ProviderPlan.Silver] = 129.90m,
        [ProviderPlan.Gold] = 199.90m
    };

    public static IReadOnlyDictionary<ProviderPlan, decimal> All => MonthlyPrices;

    public static decimal GetMonthlyPrice(ProviderPlan plan)
    {
        return MonthlyPrices.TryGetValue(plan, out var price) ? price : 0m;
    }
}
