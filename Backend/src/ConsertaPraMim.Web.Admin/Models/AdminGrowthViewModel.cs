using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminGrowthViewModel
{
    public AdminGrowthFilterModel Filters { get; set; } = new();
    public AdminGrowthFunnelDto? Funnel { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasData => Funnel != null;
}

public class AdminGrowthFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public int ProposalSlaMinutes { get; set; } = 30;
    public int AcceptanceSlaHours { get; set; } = 24;
}
