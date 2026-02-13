namespace ConsertaPraMim.Domain.Enums;

public static class ProviderPlanExtensions
{
    public static string ToPtBr(this ProviderPlan plan)
    {
        return plan switch
        {
            ProviderPlan.Trial => "Trial",
            ProviderPlan.Bronze => "Bronze",
            ProviderPlan.Silver => "Silver",
            ProviderPlan.Gold => "Gold",
            _ => plan.ToString()
        };
    }
}
