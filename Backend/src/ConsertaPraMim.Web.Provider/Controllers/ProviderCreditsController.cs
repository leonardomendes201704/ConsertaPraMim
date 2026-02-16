using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProviderCreditsController : Controller
{
    private readonly IProviderCreditService _providerCreditService;
    private readonly IProfileService _profileService;
    private readonly IPlanGovernanceService _planGovernanceService;

    public ProviderCreditsController(
        IProviderCreditService providerCreditService,
        IProfileService profileService,
        IPlanGovernanceService planGovernanceService)
    {
        _providerCreditService = providerCreditService;
        _profileService = profileService;
        _planGovernanceService = planGovernanceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ProviderCreditsFilterModel filters)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var model = new ProviderCreditsIndexViewModel
        {
            Filters = NormalizeFilters(filters)
        };

        if (!TryParseEntryType(model.Filters.EntryType, out var parsedEntryType))
        {
            model.ErrorMessage = "Filtro de tipo invalido.";
            model.Filters.EntryType = "all";
            parsedEntryType = null;
        }

        if (model.Filters.FromDate.HasValue &&
            model.Filters.ToDate.HasValue &&
            model.Filters.FromDate.Value.Date > model.Filters.ToDate.Value.Date)
        {
            model.ErrorMessage = "Periodo invalido: data inicial maior que data final.";
            return View(model);
        }

        try
        {
            model.Balance = await _providerCreditService.GetBalanceAsync(providerId, HttpContext.RequestAborted);

            var profile = await _profileService.GetProfileAsync(providerId);
            var providerPlan = profile?.ProviderProfile?.Plan;

            if (providerPlan.HasValue)
            {
                model.PlanLabel = providerPlan.Value.ToPtBr();
                model.NextBillingSimulation = await _planGovernanceService.SimulatePriceAsync(
                    new AdminPlanPriceSimulationRequestDto(
                        providerPlan.Value,
                        CouponCode: null,
                        AtUtc: DateTime.UtcNow,
                        ProviderUserId: providerId,
                        ConsumeCredits: false));
            }

            var fromUtc = model.Filters.FromDate?.Date;
            var toUtc = model.Filters.ToDate?.Date.AddDays(1).AddTicks(-1);
            var query = new ProviderCreditStatementQueryDto(
                fromUtc,
                toUtc,
                parsedEntryType,
                model.Filters.Page,
                model.Filters.PageSize);

            model.Statement = await _providerCreditService.GetStatementAsync(providerId, query, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            model.ErrorMessage = ex.Message;
        }
        catch
        {
            model.ErrorMessage = "Nao foi possivel carregar a carteira de creditos.";
        }

        return View(model);
    }

    private static ProviderCreditsFilterModel NormalizeFilters(ProviderCreditsFilterModel? filters)
    {
        var model = filters ?? new ProviderCreditsFilterModel();
        model.Page = model.Page < 1 ? 1 : model.Page;
        model.PageSize = model.PageSize <= 0 ? 20 : Math.Min(model.PageSize, 100);
        model.EntryType = string.IsNullOrWhiteSpace(model.EntryType)
            ? "all"
            : model.EntryType.Trim();
        return model;
    }

    private bool TryGetProviderId(out Guid providerId)
    {
        providerId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out providerId);
    }

    private static bool TryParseEntryType(string? raw, out ProviderCreditLedgerEntryType? entryType)
    {
        entryType = null;
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Enum.TryParse<ProviderCreditLedgerEntryType>(raw.Trim(), true, out var parsed))
        {
            entryType = parsed;
            return true;
        }

        return false;
    }
}
