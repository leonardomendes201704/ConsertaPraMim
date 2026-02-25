using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminGrowthAiController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminGrowthAiController(IAdminOperationsApiClient adminOperationsApiClient)
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
        int liquidityTake = 10)
    {
        var model = new AdminGrowthAiViewModel
        {
            AnalyzeForm = NormalizeAnalyzeForm(
                fromUtc,
                toUtc,
                category,
                city,
                proposalSlaMinutes,
                acceptanceSlaHours,
                liquidityTake),
            SuccessMessage = TempData["AdminGrowthAiSuccessMessage"]?.ToString(),
            ErrorMessage = TempData["AdminGrowthAiErrorMessage"]?.ToString()
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage ??= "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var snapshotResult = await _adminOperationsApiClient.GetGrowthAiSnapshotAsync(
            token,
            HttpContext.RequestAborted);

        if (!snapshotResult.Success || snapshotResult.Data == null)
        {
            model.ErrorMessage ??= snapshotResult.ErrorMessage ?? "Falha ao carregar configuracoes do AI Copilot.";
            return View(model);
        }

        model.Snapshot = snapshotResult.Data;
        model.SettingsForm = new AdminGrowthAiSettingsFormModel
        {
            Enabled = snapshotResult.Data.Settings.Enabled,
            Model = snapshotResult.Data.Settings.Model,
            Temperature = snapshotResult.Data.Settings.Temperature,
            MaxOutputTokens = snapshotResult.Data.Settings.MaxOutputTokens,
            SystemPrompt = snapshotResult.Data.Settings.SystemPrompt,
            ApiKeyMasked = snapshotResult.Data.Settings.ApiKeyMasked,
            IsConfigured = snapshotResult.Data.Settings.IsConfigured,
            UpdatedAtUtc = snapshotResult.Data.Settings.UpdatedAtUtc,
            LastAnalysisAtUtc = snapshotResult.Data.Settings.LastAnalysisAtUtc
        };
        model.LatestAnalysis = snapshotResult.Data.RecentAnalyses.FirstOrDefault();
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings([Bind(Prefix = "SettingsForm")] AdminGrowthAiSettingsFormModel form)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminGrowthAiErrorMessage"] = "Token administrativo nao encontrado. Faca login novamente.";
            return RedirectToAction(nameof(Index));
        }

        var request = new AdminGrowthAiUpsertSettingsRequestDto(
            Enabled: form.Enabled,
            ApiKey: string.IsNullOrWhiteSpace(form.ApiKey) ? null : form.ApiKey.Trim(),
            Model: form.Model,
            Temperature: form.Temperature,
            MaxOutputTokens: form.MaxOutputTokens,
            SystemPrompt: form.SystemPrompt);

        var result = await _adminOperationsApiClient.UpsertGrowthAiSettingsAsync(
            request,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data is { Success: false })
        {
            TempData["AdminGrowthAiErrorMessage"] = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel salvar configuracoes do AI Copilot.";
        }
        else
        {
            TempData["AdminGrowthAiSuccessMessage"] = "Configuracoes do AI Copilot salvas com sucesso.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAnalysis([Bind(Prefix = "AnalyzeForm")] AdminGrowthAiAnalyzeFormModel form)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminGrowthAiErrorMessage"] = "Token administrativo nao encontrado. Faca login novamente.";
            return RedirectToAction(nameof(Index));
        }

        var normalized = NormalizeAnalyzeForm(
            form.FromUtc,
            form.ToUtc,
            form.Category,
            form.City,
            form.ProposalSlaMinutes,
            form.AcceptanceSlaHours,
            form.LiquidityTake);

        var request = new AdminGrowthAiAnalyzeRequestDto(
            FromUtc: normalized.FromUtc,
            ToUtc: normalized.ToUtc,
            Category: normalized.Category,
            City: normalized.City,
            ProposalSlaMinutes: normalized.ProposalSlaMinutes,
            AcceptanceSlaHours: normalized.AcceptanceSlaHours,
            LiquidityTake: normalized.LiquidityTake);

        var result = await _adminOperationsApiClient.AnalyzeGrowthWithAiAsync(
            request,
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            TempData["AdminGrowthAiErrorMessage"] = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Falha ao executar analise de IA.";
        }
        else
        {
            TempData["AdminGrowthAiSuccessMessage"] = "Analise IA concluida com sucesso.";
        }

        return RedirectToAction(nameof(Index), new
        {
            fromUtc = normalized.FromUtc?.ToString("o"),
            toUtc = normalized.ToUtc?.ToString("o"),
            category = normalized.Category,
            city = normalized.City,
            proposalSlaMinutes = normalized.ProposalSlaMinutes,
            acceptanceSlaHours = normalized.AcceptanceSlaHours,
            liquidityTake = normalized.LiquidityTake
        });
    }

    private static AdminGrowthAiAnalyzeFormModel NormalizeAnalyzeForm(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes,
        int acceptanceSlaHours,
        int liquidityTake)
    {
        var normalizedFrom = fromUtc?.ToUniversalTime();
        var normalizedTo = toUtc?.ToUniversalTime();
        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return new AdminGrowthAiAnalyzeFormModel
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            ProposalSlaMinutes = Math.Clamp(proposalSlaMinutes, 5, 720),
            AcceptanceSlaHours = Math.Clamp(acceptanceSlaHours, 1, 168),
            LiquidityTake = Math.Clamp(liquidityTake, 5, 100)
        };
    }
}
