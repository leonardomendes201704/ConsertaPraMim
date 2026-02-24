using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminLiquidityScoreController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminLiquidityScoreController(IAdminOperationsApiClient adminOperationsApiClient)
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
        int take = 50)
    {
        var filters = NormalizeFilters(fromUtc, toUtc, category, city, proposalSlaMinutes, take);
        var viewModel = new AdminLiquidityScoreViewModel
        {
            Filters = filters
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(viewModel);
        }

        var result = await _adminOperationsApiClient.GetLiquidityScoreAsync(
            new AdminLiquidityScoreQueryDto(
                FromUtc: filters.FromUtc,
                ToUtc: filters.ToUtc,
                Category: filters.Category,
                City: filters.City,
                ProposalSlaMinutes: filters.ProposalSlaMinutes,
                Take: filters.Take),
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            viewModel.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar score de liquidez.";
            return View(viewModel);
        }

        viewModel.Data = result.Data;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
    }

    private static AdminLiquidityScoreFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes,
        int take)
    {
        var normalizedFrom = fromUtc?.ToUniversalTime();
        var normalizedTo = toUtc?.ToUniversalTime();

        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return new AdminLiquidityScoreFilterModel
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            ProposalSlaMinutes = Math.Clamp(proposalSlaMinutes, 5, 720),
            Take = Math.Clamp(take, 1, 200)
        };
    }
}
