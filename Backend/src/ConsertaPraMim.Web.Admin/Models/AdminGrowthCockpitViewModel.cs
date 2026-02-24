using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminGrowthCockpitViewModel
{
    public AdminGrowthCockpitFilterModel Filters { get; set; } = new();
    public AdminGrowthExecutiveCockpitDto? Cockpit { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed class AdminGrowthCockpitFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public int ProposalSlaMinutes { get; set; } = 30;
    public int AcceptanceSlaHours { get; set; } = 24;
    public int NorthStarResolutionHours { get; set; } = 72;
}
