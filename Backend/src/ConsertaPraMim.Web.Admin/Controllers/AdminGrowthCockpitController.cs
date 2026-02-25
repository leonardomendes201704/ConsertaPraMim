using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminGrowthCockpitController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;
    private readonly IAdminRoadmapService _adminRoadmapService;

    public AdminGrowthCockpitController(
        IAdminOperationsApiClient adminOperationsApiClient,
        IAdminRoadmapService adminRoadmapService)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
        _adminRoadmapService = adminRoadmapService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes = 30,
        int acceptanceSlaHours = 24,
        int northStarResolutionHours = 72)
    {
        var filters = NormalizeFilters(
            fromUtc,
            toUtc,
            category,
            city,
            proposalSlaMinutes,
            acceptanceSlaHours,
            northStarResolutionHours);

        var model = new AdminGrowthCockpitViewModel
        {
            Filters = filters
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetGrowthExecutiveCockpitAsync(
            new AdminGrowthExecutiveCockpitQueryDto(
                FromUtc: filters.FromUtc,
                ToUtc: filters.ToUtc,
                Category: filters.Category,
                City: filters.City,
                ProposalSlaMinutes: filters.ProposalSlaMinutes,
                AcceptanceSlaHours: filters.AcceptanceSlaHours,
                NorthStarResolutionHours: filters.NorthStarResolutionHours),
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar cockpit executivo de growth.";
            return View(model);
        }

        var weeklyRitualResult = await _adminOperationsApiClient.GetGrowthWeeklyRitualAsync(
            filters.ToUtc,
            token,
            HttpContext.RequestAborted);

        if (weeklyRitualResult.Success && weeklyRitualResult.Data != null)
        {
            model.WeeklyRitualSnapshot = weeklyRitualResult.Data;
        }
        else
        {
            model.WeeklyRitualErrorMessage = weeklyRitualResult.ErrorMessage ?? "Falha ao carregar ritual semanal.";
        }

        var monthlyReviewResult = await _adminOperationsApiClient.GetGrowthMonthlyReviewAsync(
            filters.ToUtc,
            token,
            HttpContext.RequestAborted);

        if (monthlyReviewResult.Success && monthlyReviewResult.Data != null)
        {
            model.MonthlyReviewSnapshot = monthlyReviewResult.Data;
        }
        else
        {
            model.MonthlyReviewErrorMessage = monthlyReviewResult.ErrorMessage ?? "Falha ao carregar revisao mensal.";
        }

        var roadmapResult = await _adminRoadmapService.BuildViewModelAsync(
            searchTerm: null,
            epicFilter: null,
            trackFilter: null,
            statusFilter: null,
            HttpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(roadmapResult.ErrorMessage))
        {
            model.RoadmapSnapshot = BuildRoadmapSnapshot(roadmapResult);
        }
        else
        {
            model.RoadmapErrorMessage = roadmapResult.ErrorMessage;
        }

        ApplyFeedback(
            tempDataKey: "GrowthWeeklyRitualFeedback",
            onSuccess: (success, message) =>
            {
                model.WeeklyRitualFeedbackSuccess = success;
                model.WeeklyRitualFeedbackMessage = message;
            });

        ApplyFeedback(
            tempDataKey: "GrowthMonthlyReviewFeedback",
            onSuccess: (success, message) =>
            {
                model.MonthlyReviewFeedbackSuccess = success;
                model.MonthlyReviewFeedbackMessage = message;
            });

        model.Cockpit = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordWeeklyRitual(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes = 30,
        int acceptanceSlaHours = 24,
        int northStarResolutionHours = 72,
        string? summary = null,
        string? decisions = null,
        string? ownerActions = null,
        string? risks = null,
        string? nextActions = null)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["GrowthWeeklyRitualFeedback"] = JsonSerializer.Serialize(
                new WeeklyRitualFeedback(false, "Token administrativo nao encontrado. Faca login novamente."),
                JsonOptions);
            return RedirectToAction(nameof(Index), new
            {
                fromUtc,
                toUtc,
                category,
                city,
                proposalSlaMinutes,
                acceptanceSlaHours,
                northStarResolutionHours
            });
        }

        var request = new AdminGrowthWeeklyRitualRecordRequestDto(
            Summary: summary ?? string.Empty,
            Decisions: decisions ?? string.Empty,
            OwnerActions: ownerActions ?? string.Empty,
            Risks: risks ?? string.Empty,
            NextActions: nextActions ?? string.Empty);

        var result = await _adminOperationsApiClient.RecordGrowthWeeklyRitualAsync(
            request,
            token,
            HttpContext.RequestAborted);

        TempData["GrowthWeeklyRitualFeedback"] = JsonSerializer.Serialize(
            result.Success
                ? new WeeklyRitualFeedback(true, "Ata semanal registrada com sucesso.")
                : new WeeklyRitualFeedback(false, result.ErrorMessage ?? "Falha ao registrar ata semanal."),
            JsonOptions);

        return RedirectToAction(nameof(Index), new
        {
            fromUtc,
            toUtc,
            category,
            city,
            proposalSlaMinutes,
            acceptanceSlaHours,
            northStarResolutionHours
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordMonthlyReview(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes = 30,
        int acceptanceSlaHours = 24,
        int northStarResolutionHours = 72,
        DateTime? referenceMonthUtc = null,
        string? executiveSummary = null,
        string? strategicDecisions = null,
        string? risksAndBlockers = null,
        string? nextMonthBets = null,
        string? budgetNotes = null)
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["GrowthMonthlyReviewFeedback"] = JsonSerializer.Serialize(
                new WeeklyRitualFeedback(false, "Token administrativo nao encontrado. Faca login novamente."),
                JsonOptions);
            return RedirectToAction(nameof(Index), new
            {
                fromUtc,
                toUtc,
                category,
                city,
                proposalSlaMinutes,
                acceptanceSlaHours,
                northStarResolutionHours
            });
        }

        var request = new AdminGrowthMonthlyReviewRecordRequestDto(
            ReferenceMonthUtc: referenceMonthUtc,
            ExecutiveSummary: executiveSummary ?? string.Empty,
            StrategicDecisions: strategicDecisions ?? string.Empty,
            RisksAndBlockers: risksAndBlockers ?? string.Empty,
            NextMonthBets: nextMonthBets ?? string.Empty,
            BudgetNotes: budgetNotes ?? string.Empty);

        var result = await _adminOperationsApiClient.RecordGrowthMonthlyReviewAsync(
            request,
            token,
            HttpContext.RequestAborted);

        TempData["GrowthMonthlyReviewFeedback"] = JsonSerializer.Serialize(
            result.Success
                ? new WeeklyRitualFeedback(true, "Revisao mensal registrada com sucesso.")
                : new WeeklyRitualFeedback(false, result.ErrorMessage ?? "Falha ao registrar revisao mensal."),
            JsonOptions);

        return RedirectToAction(nameof(Index), new
        {
            fromUtc,
            toUtc,
            category,
            city,
            proposalSlaMinutes,
            acceptanceSlaHours,
            northStarResolutionHours
        });
    }

    private static AdminGrowthCockpitFilterModel NormalizeFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? category,
        string? city,
        int proposalSlaMinutes,
        int acceptanceSlaHours,
        int northStarResolutionHours)
    {
        var normalizedFrom = fromUtc?.ToUniversalTime();
        var normalizedTo = toUtc?.ToUniversalTime();

        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return new AdminGrowthCockpitFilterModel
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            ProposalSlaMinutes = Math.Clamp(proposalSlaMinutes, 5, 720),
            AcceptanceSlaHours = Math.Clamp(acceptanceSlaHours, 1, 168),
            NorthStarResolutionHours = Math.Clamp(northStarResolutionHours, 24, 240)
        };
    }

    private static AdminGrowthRoadmapSnapshotViewModel BuildRoadmapSnapshot(AdminRoadmapViewModel roadmap)
    {
        var totalStories = Math.Max(roadmap.TotalStories, 0);
        var doneStories = Math.Max(roadmap.DoneStories, 0);
        var inProgressStories = Math.Max(roadmap.InProgressStories, 0);

        var deliveryRate = totalStories <= 0
            ? 0d
            : Math.Round(doneStories * 100d / totalStories, 2, MidpointRounding.AwayFromZero);

        var inProgressRate = totalStories <= 0
            ? 0d
            : Math.Round(inProgressStories * 100d / totalStories, 2, MidpointRounding.AwayFromZero);

        // Prioriza stories em execucao e, na sequencia, backlog com mais tarefas pendentes.
        var priorityStories = roadmap.StoriesInProgress
            .Concat(roadmap.StoriesBacklog)
            .OrderByDescending(story => story.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(story => Math.Max(story.TasksTotal - story.TasksDone, 0))
            .ThenBy(story => story.StoryId, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(story => new AdminGrowthRoadmapStorySummaryViewModel
            {
                StoryId = story.StoryId,
                Title = story.Title,
                Status = story.Status,
                EpicId = story.EpicId,
                Track = story.Track,
                TasksDone = story.TasksDone,
                TasksTotal = story.TasksTotal,
                WikiRelativePath = story.WikiRelativePath
            })
            .ToArray();

        return new AdminGrowthRoadmapSnapshotViewModel
        {
            TotalStories = totalStories,
            BacklogStories = Math.Max(roadmap.BacklogStories, 0),
            InProgressStories = inProgressStories,
            DoneStories = doneStories,
            DeliveryRatePercent = deliveryRate,
            InProgressRatePercent = inProgressRate,
            PriorityStories = priorityStories
        };
    }

    private void ApplyFeedback(string tempDataKey, Action<bool, string> onSuccess)
    {
        if (!TempData.TryGetValue(tempDataKey, out var feedbackRaw) ||
            feedbackRaw is not string feedbackJson ||
            string.IsNullOrWhiteSpace(feedbackJson))
        {
            return;
        }

        try
        {
            var feedback = JsonSerializer.Deserialize<WeeklyRitualFeedback>(feedbackJson, JsonOptions);
            if (feedback != null)
            {
                onSuccess(feedback.Success, feedback.Message);
            }
        }
        catch (JsonException)
        {
            // no-op
        }
    }

    private sealed record WeeklyRitualFeedback(bool Success, string Message);
}
