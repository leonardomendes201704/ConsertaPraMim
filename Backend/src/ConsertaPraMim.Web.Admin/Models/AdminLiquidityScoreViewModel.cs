using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminLiquidityScoreViewModel
{
    public AdminLiquidityScoreFilterModel Filters { get; set; } = new();
    public AdminLiquidityScoreResponseDto? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasData => Data != null;
}

public class AdminLiquidityScoreFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public int ProposalSlaMinutes { get; set; } = 30;
    public int Take { get; set; } = 50;
}
