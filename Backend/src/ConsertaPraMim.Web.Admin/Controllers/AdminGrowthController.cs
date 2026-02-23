using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminGrowthController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminGrowthController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes = 30,
        int acceptanceSlaHours = 24)
    {
        var filters = NormalizeFilters(fromUtc, toUtc, category, city, proposalSlaMinutes, acceptanceSlaHours);
        var model = new AdminGrowthViewModel
        {
            Filters = filters
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetGrowthFunnelAsync(
            new AdminGrowthFunnelQueryDto(
                FromUtc: filters.FromUtc,
                ToUtc: filters.ToUtc,
                Category: filters.Category,
                City: filters.City,
                ProposalSlaMinutes: filters.ProposalSlaMinutes,
                AcceptanceSlaHours: filters.AcceptanceSlaHours),
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar funil de growth.";
            return View(model);
        }

        model.Funnel = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    private static AdminGrowthFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes,
        int acceptanceSlaHours)
    {
        var normalizedFrom = fromUtc?.ToUniversalTime();
        var normalizedTo = toUtc?.ToUniversalTime();

        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return new AdminGrowthFilterModel
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            ProposalSlaMinutes = Math.Clamp(proposalSlaMinutes, 5, 720),
            AcceptanceSlaHours = Math.Clamp(acceptanceSlaHours, 1, 168)
        };
    }
}
