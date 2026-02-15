using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminHomeController : Controller
{
    private readonly IAdminDashboardApiClient _adminDashboardApiClient;

    public AdminHomeController(IAdminDashboardApiClient adminDashboardApiClient)
    {
        _adminDashboardApiClient = adminDashboardApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        string? operationalStatus,
        string? noShowCity,
        string? noShowCategory,
        string? noShowRiskLevel,
        int noShowQueueTake = 50,
        int noShowCancellationNoShowWindowHours = 24,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20)
    {
        var filters = NormalizeFilters(
            fromUtc,
            toUtc,
            eventType,
            operationalStatus,
            noShowCity,
            noShowCategory,
            noShowRiskLevel,
            noShowQueueTake,
            noShowCancellationNoShowWindowHours,
            searchTerm,
            page,
            pageSize);
        var viewModel = new AdminDashboardViewModel
        {
            Filters = filters
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(viewModel);
        }

        var dashboardResult = await _adminDashboardApiClient.GetDashboardAsync(filters, token, HttpContext.RequestAborted);
        if (!dashboardResult.Success || dashboardResult.Dashboard == null)
        {
            viewModel.ErrorMessage = dashboardResult.ErrorMessage ?? "Falha ao carregar dashboard administrativo.";
            return View(viewModel);
        }

        viewModel.Dashboard = dashboardResult.Dashboard;
        var noShowResult = await _adminDashboardApiClient.GetNoShowDashboardAsync(filters, token, HttpContext.RequestAborted);
        if (noShowResult.Success && noShowResult.Dashboard != null)
        {
            viewModel.NoShowDashboard = noShowResult.Dashboard;
        }
        else
        {
            viewModel.NoShowErrorMessage = noShowResult.ErrorMessage ?? "Falha ao carregar painel de no-show.";
        }

        var noShowThresholdResult = await _adminDashboardApiClient.GetNoShowAlertThresholdsAsync(token, HttpContext.RequestAborted);
        if (noShowThresholdResult.Success && noShowThresholdResult.Configuration != null)
        {
            viewModel.NoShowAlertThresholds = noShowThresholdResult.Configuration;
        }
        else
        {
            viewModel.NoShowThresholdErrorMessage = noShowThresholdResult.ErrorMessage ?? "Falha ao carregar thresholds de no-show.";
        }

        viewModel.LastUpdatedUtc = DateTime.UtcNow;

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Snapshot(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        string? operationalStatus,
        string? noShowCity,
        string? noShowCategory,
        string? noShowRiskLevel,
        int noShowQueueTake = 50,
        int noShowCancellationNoShowWindowHours = 24,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Token administrativo ausente. Faca login novamente."
            });
        }

        var filters = NormalizeFilters(
            fromUtc,
            toUtc,
            eventType,
            operationalStatus,
            noShowCity,
            noShowCategory,
            noShowRiskLevel,
            noShowQueueTake,
            noShowCancellationNoShowWindowHours,
            searchTerm,
            page,
            pageSize);
        var dashboardResult = await _adminDashboardApiClient.GetDashboardAsync(filters, token, HttpContext.RequestAborted);

        if (!dashboardResult.Success || dashboardResult.Dashboard == null)
        {
            var statusCode = dashboardResult.StatusCode ?? StatusCodes.Status502BadGateway;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = dashboardResult.ErrorMessage ?? "Falha ao carregar dashboard."
            });
        }

        var noShowResult = await _adminDashboardApiClient.GetNoShowDashboardAsync(filters, token, HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            data = dashboardResult.Dashboard,
            noShowData = noShowResult.Success ? noShowResult.Dashboard : null,
            noShowErrorMessage = noShowResult.Success ? null : noShowResult.ErrorMessage,
            refreshedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateNoShowThresholds([FromBody] AdminUpdateNoShowAlertThresholdWebRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { success = false, errorMessage = "Payload invalido." });
        }

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateNoShowAlertThresholdRequestDto(
            request.NoShowRateWarningPercent,
            request.NoShowRateCriticalPercent,
            request.HighRiskQueueWarningCount,
            request.HighRiskQueueCriticalCount,
            request.ReminderSendSuccessWarningPercent,
            request.ReminderSendSuccessCriticalPercent,
            request.Notes);

        var result = await _adminDashboardApiClient.UpdateNoShowAlertThresholdsAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Configuration == null)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status502BadGateway;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Falha ao atualizar thresholds de no-show."
            });
        }

        return Ok(new
        {
            success = true,
            configuration = result.Configuration
        });
    }

    private static AdminDashboardFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        string? operationalStatus,
        string? noShowCity,
        string? noShowCategory,
        string? noShowRiskLevel,
        int noShowQueueTake,
        int noShowCancellationNoShowWindowHours,
        string? searchTerm,
        int page,
        int pageSize)
    {
        DateTime? normalizedFrom = fromUtc?.ToUniversalTime();
        DateTime? normalizedTo = toUtc?.ToUniversalTime();

        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return new AdminDashboardFilterModel
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            EventType = NormalizeEventType(eventType),
            OperationalStatus = NormalizeOperationalStatus(operationalStatus),
            NoShowCity = string.IsNullOrWhiteSpace(noShowCity) ? null : noShowCity.Trim(),
            NoShowCategory = string.IsNullOrWhiteSpace(noShowCategory) ? null : noShowCategory.Trim(),
            NoShowRiskLevel = NormalizeNoShowRiskLevel(noShowRiskLevel),
            NoShowQueueTake = Math.Clamp(noShowQueueTake, 1, 500),
            NoShowCancellationWindowHours = Math.Clamp(noShowCancellationNoShowWindowHours, 1, 168),
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }

    private static string NormalizeEventType(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return "all";
        }

        var normalized = eventType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "request" => "request",
            "proposal" => "proposal",
            "chat" => "chat",
            _ => "all"
        };
    }

    private static string NormalizeOperationalStatus(string? operationalStatus)
    {
        if (string.IsNullOrWhiteSpace(operationalStatus))
        {
            return "all";
        }

        return ServiceAppointmentOperationalStatusExtensions.TryParseFlexible(operationalStatus, out var parsed)
            ? parsed.ToString()
            : "all";
    }

    private static string NormalizeNoShowRiskLevel(string? noShowRiskLevel)
    {
        if (string.IsNullOrWhiteSpace(noShowRiskLevel))
        {
            return "all";
        }

        return Enum.TryParse<ServiceAppointmentNoShowRiskLevel>(noShowRiskLevel.Trim(), true, out var parsed)
            ? parsed.ToString()
            : "all";
    }
}
