using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminDashboardViewModel
{
    public AdminDashboardFilterModel Filters { get; set; } = new();
    public AdminDashboardDto? Dashboard { get; set; }
    public AdminNoShowDashboardDto? NoShowDashboard { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NoShowErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasData => Dashboard != null;
    public bool HasNoShowData => NoShowDashboard != null;
    public bool IsEmpty => Dashboard?.RecentEvents == null || Dashboard.RecentEvents.Count == 0;
}

public class AdminDashboardFilterModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string EventType { get; set; } = "all";
    public string OperationalStatus { get; set; } = "all";
    public string? SearchTerm { get; set; }
    public string? NoShowCity { get; set; }
    public string? NoShowCategory { get; set; }
    public string NoShowRiskLevel { get; set; } = "all";
    public int NoShowQueueTake { get; set; } = 50;
    public int NoShowCancellationWindowHours { get; set; } = 24;
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

public class AdminNoShowDashboardApiResult
{
    public bool Success { get; init; }
    public AdminNoShowDashboardDto? Dashboard { get; init; }
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }

    public static AdminNoShowDashboardApiResult Ok(AdminNoShowDashboardDto dashboard)
        => new() { Success = true, Dashboard = dashboard };

    public static AdminNoShowDashboardApiResult Fail(string message, int? statusCode = null)
        => new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}

public class AdminNoShowAlertThresholdApiResult
{
    public bool Success { get; init; }
    public AdminNoShowAlertThresholdDto? Configuration { get; init; }
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }

    public static AdminNoShowAlertThresholdApiResult Ok(AdminNoShowAlertThresholdDto configuration)
        => new() { Success = true, Configuration = configuration };

    public static AdminNoShowAlertThresholdApiResult Fail(string message, int? statusCode = null)
        => new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}

public class AdminCoverageMapApiResult
{
    public bool Success { get; init; }
    public AdminCoverageMapDto? CoverageMap { get; init; }
    public string? ErrorMessage { get; init; }
    public int? StatusCode { get; init; }

    public static AdminCoverageMapApiResult Ok(AdminCoverageMapDto coverageMap)
        => new() { Success = true, CoverageMap = coverageMap };

    public static AdminCoverageMapApiResult Fail(string message, int? statusCode = null)
        => new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}

public class AdminUpdateNoShowAlertThresholdWebRequest
{
    public decimal NoShowRateWarningPercent { get; set; }
    public decimal NoShowRateCriticalPercent { get; set; }
    public int HighRiskQueueWarningCount { get; set; }
    public int HighRiskQueueCriticalCount { get; set; }
    public decimal ReminderSendSuccessWarningPercent { get; set; }
    public decimal ReminderSendSuccessCriticalPercent { get; set; }
    public string? Notes { get; set; }
}
