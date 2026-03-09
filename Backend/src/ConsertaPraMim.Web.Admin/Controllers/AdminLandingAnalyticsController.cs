using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminLandingAnalyticsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminLandingAnalyticsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? origin,
        string? path,
        string? countryCode,
        string? region,
        string? city,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 20,
        bool includeSuspectedAutomation = false)
    {
        var model = new AdminLandingAnalyticsIndexViewModel
        {
            Filters = NormalizeFilters(searchTerm, origin, path, countryCode, region, city, includeSuspectedAutomation, fromUtc, toUtc, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetLandingAnalyticsAsync(model.Filters, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar analytics da landing.";
            return View(model);
        }

        model.Overview = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string sessionId)
    {
        var model = new AdminLandingAnalyticsDetailsViewModel
        {
            SessionId = sessionId?.Trim() ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(model.SessionId))
        {
            return NotFound();
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetLandingAnalyticsSessionDetailsAsync(model.SessionId, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar a sessao.";
            return View(model);
        }

        model.Details = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminLandingAnalyticsFilterModel NormalizeFilters(
        string? searchTerm,
        string? origin,
        string? path,
        string? countryCode,
        string? region,
        string? city,
        bool includeSuspectedAutomation,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        var from = NormalizeDateBoundary(fromUtc, endOfDay: false) ?? DateTime.UtcNow.AddDays(-30);
        var to = NormalizeDateBoundary(toUtc, endOfDay: true) ?? DateTime.UtcNow;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        return new AdminLandingAnalyticsFilterModel
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Origin = NormalizeOrigin(origin),
            Path = string.IsNullOrWhiteSpace(path) ? null : path.Trim(),
            CountryCode = NormalizeCountryCode(countryCode),
            Region = string.IsNullOrWhiteSpace(region) ? null : region.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            IncludeSuspectedAutomation = includeSuspectedAutomation,
            FromUtc = from,
            ToUtc = to,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }

    private static DateTime? NormalizeDateBoundary(DateTime? value, bool endOfDay)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalizedDate = DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
        return endOfDay
            ? normalizedDate.AddDays(1).AddTicks(-1)
            : normalizedDate;
    }

    private static string NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return "all";
        }

        return origin.Trim().ToLowerInvariant() switch
        {
            "client" => "Client",
            "provider" => "Provider",
            _ => "all"
        };
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();
        return normalized.Length <= 8 ? normalized : normalized[..8];
    }
}
