using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminDashboardViewModel
{
    public AdminDashboardFilterModel Filters { get; set; } = new();
    public AdminDashboardDto? Dashboard { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasData => Dashboard != null;
    public bool IsEmpty => Dashboard?.RecentEvents == null || Dashboard.RecentEvents.Count == 0;
}

public class AdminDashboardFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string EventType { get; set; } = "all";
    public string OperationalStatus { get; set; } = "all";
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminDashboardApiResult
{
    public bool Success { get; init; }
    public AdminDashboardDto? Dashboard { get; init; }
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }

    public static AdminDashboardApiResult Ok(AdminDashboardDto dashboard)
        => new() { Success = true, Dashboard = dashboard };

    public static AdminDashboardApiResult Fail(string message, int? statusCode = null)
        => new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}
