using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
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
        string? searchTerm,
        int page = 1,
        int pageSize = 20)
    {
        var filters = NormalizeFilters(fromUtc, toUtc, eventType, operationalStatus, searchTerm, page, pageSize);
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
        viewModel.LastUpdatedUtc = DateTime.UtcNow;

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Snapshot(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        string? operationalStatus,
        string? searchTerm,
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

        var filters = NormalizeFilters(fromUtc, toUtc, eventType, operationalStatus, searchTerm, page, pageSize);
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

        return Ok(new
        {
            success = true,
            data = dashboardResult.Dashboard,
            refreshedAtUtc = DateTime.UtcNow
        });
    }

    private static AdminDashboardFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        string? operationalStatus,
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
}
