using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Models;

public class ProviderCreditsFilterModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string EntryType { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProviderCreditsIndexViewModel
{
    public ProviderCreditsFilterModel Filters { get; set; } = new();
    public ProviderCreditBalanceDto? Balance { get; set; }
    public ProviderCreditStatementDto? Statement { get; set; }
    public AdminPlanPriceSimulationResultDto? NextBillingSimulation { get; set; }
    public string? PlanLabel { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int TotalPages
    {
        get
        {
            if (Statement == null || Statement.PageSize <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling((double)Statement.TotalCount / Statement.PageSize);
        }
    }
}
