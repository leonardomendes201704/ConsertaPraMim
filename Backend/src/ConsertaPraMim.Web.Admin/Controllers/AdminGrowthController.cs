using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminGrowthController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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
        int acceptanceSlaHours = 24,
        DateTime? reactivationAsOfUtc = null,
        int reactivationWarmFromDays = 7,
        int reactivationColdFromDays = 15,
        int reactivationDormantFromDays = 31,
        int reactivationHibernatedFromDays = 61,
        int reactivationPreviewTake = 50)
    {
        var filters = NormalizeFilters(
            fromUtc,
            toUtc,
            category,
            city,
            proposalSlaMinutes,
            acceptanceSlaHours,
            reactivationAsOfUtc,
            reactivationWarmFromDays,
            reactivationColdFromDays,
            reactivationDormantFromDays,
            reactivationHibernatedFromDays,
            reactivationPreviewTake);
        var model = new AdminGrowthViewModel
        {
            Filters = filters
        };

        if (TempData.TryGetValue("ProviderReactivationCampaignResult", out var campaignResultRaw) &&
            campaignResultRaw is string campaignResultJson &&
            !string.IsNullOrWhiteSpace(campaignResultJson))
        {
            try
            {
                model.LastCampaignRun = JsonSerializer.Deserialize<AdminProviderReactivationCampaignRunResultDto>(campaignResultJson, JsonOptions);
            }
            catch (JsonException)
            {
                // no-op: feedback panel remains hidden when payload is invalid.
            }
        }

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
        var reactivationResult = await _adminOperationsApiClient.GetProviderReactivationSegmentsAsync(
            new AdminProviderReactivationSegmentsQueryDto(
                AsOfUtc: filters.ReactivationAsOfUtc,
                WarmFromDays: filters.ReactivationWarmFromDays,
                ColdFromDays: filters.ReactivationColdFromDays,
                DormantFromDays: filters.ReactivationDormantFromDays,
                HibernatedFromDays: filters.ReactivationHibernatedFromDays,
                PreviewTake: filters.ReactivationPreviewTake),
            token,
            HttpContext.RequestAborted);

        if (reactivationResult.Success && reactivationResult.Data != null)
        {
            model.ProviderReactivationSegments = reactivationResult.Data;
        }
        else
        {
            model.ProviderReactivationErrorMessage = reactivationResult.ErrorMessage ?? "Falha ao carregar segmentos de reativacao.";
        }

        var performanceResult = await _adminOperationsApiClient.GetProviderReactivationCampaignPerformanceAsync(
            new AdminProviderReactivationCampaignPerformanceQueryDto(
                FromUtc: filters.FromUtc,
                ToUtc: filters.ToUtc,
                Take: 50),
            token,
            HttpContext.RequestAborted);

        if (performanceResult.Success && performanceResult.Data != null)
        {
            model.CampaignPerformance = performanceResult.Data;
        }
        else
        {
            model.CampaignPerformanceErrorMessage = performanceResult.ErrorMessage ?? "Falha ao carregar performance das campanhas.";
        }

        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunReactivationCampaign(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes = 30,
        int acceptanceSlaHours = 24,
        DateTime? reactivationAsOfUtc = null,
        int reactivationWarmFromDays = 7,
        int reactivationColdFromDays = 15,
        int reactivationDormantFromDays = 31,
        int reactivationHibernatedFromDays = 61,
        int reactivationPreviewTake = 50,
        int campaignCadenceHours = 24,
        int campaignMaxRecipients = 200,
        bool campaignForceRun = false,
        string? campaignSegmentCode = null,
        bool campaignSendSystem = true,
        bool campaignSendPush = true,
        bool campaignSendEmail = false,
        string? campaignMessageTemplate = null)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (!string.IsNullOrWhiteSpace(token))
        {
            var result = await _adminOperationsApiClient.RunProviderReactivationCampaignAsync(
                new AdminProviderReactivationCampaignRunRequestDto(
                    AsOfUtc: reactivationAsOfUtc,
                    CadenceHours: campaignCadenceHours,
                    MaxRecipients: campaignMaxRecipients,
                    ForceRun: campaignForceRun,
                    SegmentCode: campaignSegmentCode,
                    SendSystem: campaignSendSystem,
                    SendPush: campaignSendPush,
                    SendEmail: campaignSendEmail,
                    MessageTemplate: campaignMessageTemplate),
                token,
                HttpContext.RequestAborted);

            if (result.Success && result.Data != null)
            {
                TempData["ProviderReactivationCampaignResult"] = JsonSerializer.Serialize(result.Data, JsonOptions);
            }
            else
            {
                var errorPayload = new AdminProviderReactivationCampaignRunResultDto(
                    CampaignId: Guid.Empty,
                    RequestedAtUtc: DateTime.UtcNow,
                    Executed: false,
                    Status: "failed",
                    Message: result.ErrorMessage ?? "Falha ao executar campanha de reativacao.",
                    CadenceHours: campaignCadenceHours,
                    ForceRun: campaignForceRun,
                    SelectedProviders: 0,
                    SegmentCode: campaignSegmentCode,
                    PreviousCampaignAtUtc: null,
                    Recipients: Array.Empty<AdminProviderReactivationProviderPreviewDto>());
                TempData["ProviderReactivationCampaignResult"] = JsonSerializer.Serialize(errorPayload, JsonOptions);
            }
        }

        return RedirectToAction(nameof(Index), new
        {
            fromUtc,
            toUtc,
            category,
            city,
            proposalSlaMinutes,
            acceptanceSlaHours,
            reactivationAsOfUtc,
            reactivationWarmFromDays,
            reactivationColdFromDays,
            reactivationDormantFromDays,
            reactivationHibernatedFromDays,
            reactivationPreviewTake
        });
    }

    private static AdminGrowthFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes,
        int acceptanceSlaHours,
        DateTime? reactivationAsOfUtc,
        int reactivationWarmFromDays,
        int reactivationColdFromDays,
        int reactivationDormantFromDays,
        int reactivationHibernatedFromDays,
        int reactivationPreviewTake)
    {
        var normalizedFrom = fromUtc?.ToUniversalTime();
        var normalizedTo = toUtc?.ToUniversalTime();
        var normalizedReactivationAsOf = reactivationAsOfUtc?.ToUniversalTime();

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
            AcceptanceSlaHours = Math.Clamp(acceptanceSlaHours, 1, 168),
            ReactivationAsOfUtc = normalizedReactivationAsOf,
            ReactivationWarmFromDays = Math.Clamp(reactivationWarmFromDays, 1, 365),
            ReactivationColdFromDays = Math.Clamp(reactivationColdFromDays, 2, 365),
            ReactivationDormantFromDays = Math.Clamp(reactivationDormantFromDays, 3, 730),
            ReactivationHibernatedFromDays = Math.Clamp(reactivationHibernatedFromDays, 4, 1460),
            ReactivationPreviewTake = Math.Clamp(reactivationPreviewTake, 10, 200)
        };
    }
}
