using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminLandingLeadsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminLandingLeadsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? origin,
        string? city,
        string? state,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminLandingLeadsIndexViewModel
        {
            Filters = NormalizeFilters(searchTerm, origin, city, state, fromUtc, toUtc, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetLandingLeadsAsync(model.Filters, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar leads da landing.";
            return View(model);
        }

        model.Leads = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = new AdminLandingLeadDetailsViewModel();
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetLandingLeadByIdAsync(id, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar detalhes do lead.";
            return View(model);
        }

        model.Lead = result.Data;
        return View(model);
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminLandingLeadsFilterModel NormalizeFilters(
        string? searchTerm,
        string? origin,
        string? city,
        string? state,
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

        return new AdminLandingLeadsFilterModel
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Origin = NormalizeOrigin(origin),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            State = NormalizeState(state),
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
        if (string.IsNullOrWhiteSpace(origin)) return "all";

        var normalized = origin.Trim().ToLowerInvariant();
        return normalized switch
        {
            "client" => "Client",
            "provider" => "Provider",
            _ => "all"
        };
    }

    private static string? NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var normalized = state.Trim().ToUpperInvariant();
        return normalized.Length > 2 ? normalized[..2] : normalized;
    }
}
