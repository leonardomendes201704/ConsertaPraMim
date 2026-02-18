using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Models;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class HomeController : Controller
{
    private const int DashboardRecentMatchesLimit = 15;
    private const int DefaultMapPinPageSize = 120;
    private const int MinMapPinPageSize = 20;
    private const int MaxMapPinPageSize = 200;

    private readonly ILogger<HomeController> _logger;
    private readonly IProviderBackendApiClient _backendApiClient;

    public HomeController(
        ILogger<HomeController> logger,
        IProviderBackendApiClient backendApiClient)
    {
        _logger = logger;
        _backendApiClient = backendApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");
        var userId = Guid.Parse(userIdString);

        var cancellationToken = HttpContext.RequestAborted;
        var profileTask = _backendApiClient.GetProfileAsync(cancellationToken);
        var matchesTask = _backendApiClient.GetRequestsAsync(cancellationToken: cancellationToken);
        var proposalsTask = _backendApiClient.GetMyProposalsAsync(cancellationToken);
        var historyTask = _backendApiClient.GetHistoryAsync(cancellationToken);
        var appointmentsTask = _backendApiClient.GetMyAppointmentsAsync(cancellationToken);

        await Task.WhenAll(profileTask, matchesTask, proposalsTask, historyTask, appointmentsTask);

        var (profile, profileError) = profileTask.Result;
        var (matches, matchesError) = matchesTask.Result;
        var (myProposals, proposalsError) = proposalsTask.Result;
        var (history, historyError) = historyTask.Result;
        var (appointments, appointmentsError) = appointmentsTask.Result;

        var firstError = profileError ?? matchesError ?? proposalsError ?? historyError ?? appointmentsError;
        if (!string.IsNullOrWhiteSpace(firstError))
        {
            TempData["Error"] = firstError;
        }

        var providerProfile = profile?.ProviderProfile;
        var nowUtc = DateTime.UtcNow;
        var pendingAppointments = appointments.Count(a =>
            string.Equals(a.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase));
        var upcomingConfirmedVisits = appointments.Count(a =>
            (string.Equals(a.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase)) &&
            a.WindowStartUtc >= nowUtc);

        ViewBag.TotalMatches = matches.Count;
        ViewBag.ActiveProposals = myProposals.Count(p => !p.Accepted);
        ViewBag.ConvertedJobs = myProposals.Count(p => p.Accepted);
        ViewBag.PendingAppointments = pendingAppointments;
        ViewBag.UpcomingConfirmedVisits = upcomingConfirmedVisits;
        ViewBag.TotalRevenue = history.Sum(h => h.EstimatedValue ?? 0m);
        ViewBag.AverageTicket = history.Any() ? history.Average(h => (double)(h.EstimatedValue ?? 0m)) : 0d;
        ViewBag.ProviderOperationalStatus = providerProfile?.OperationalStatus.ToString() ?? "Online";
        ViewBag.ProviderAvatarUrl = ResolveProviderAvatarUrl(profile);
        ViewBag.RecentMatchesLimit = DashboardRecentMatchesLimit;

        var coverageMap = await BuildCoverageMapPayloadAsync(cancellationToken: cancellationToken);
        ViewBag.TotalMatches = coverageMap.TotalPins;
        ViewBag.ProviderCoverageMapJson = JsonSerializer.Serialize(coverageMap);

        return View(matches.Take(DashboardRecentMatchesLimit));
    }

    [HttpGet]
    public async Task<IActionResult> RecentMatchesData(
        string? category = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = DefaultMapPinPageSize)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString)) return Unauthorized();

        var cancellationToken = HttpContext.RequestAborted;
        var profileTask = _backendApiClient.GetProfileAsync(cancellationToken);
        var proposalsTask = _backendApiClient.GetMyProposalsAsync(cancellationToken);
        var appointmentsTask = _backendApiClient.GetMyAppointmentsAsync(cancellationToken);
        var coverageTask = BuildCoverageMapPayloadAsync(category, maxDistanceKm, pinPage, pinPageSize, cancellationToken);

        await Task.WhenAll(profileTask, proposalsTask, appointmentsTask, coverageTask);

        var (profile, profileError) = profileTask.Result;
        var (myProposals, proposalsError) = proposalsTask.Result;
        var (appointments, appointmentsError) = appointmentsTask.Result;
        var coverageMap = coverageTask.Result;

        var firstError = profileError ?? proposalsError ?? appointmentsError;
        if (!string.IsNullOrWhiteSpace(firstError))
        {
            return BadRequest(new { message = firstError });
        }

        var nowUtc = DateTime.UtcNow;
        var pendingAppointments = appointments.Count(a =>
            string.Equals(a.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase));
        var upcomingConfirmedVisits = appointments.Count(a =>
            (string.Equals(a.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase)) &&
            a.WindowStartUtc >= nowUtc);

        var recentMatches = coverageMap.Pins
            .OrderBy(pin => pin.DistanceKm)
            .Take(DashboardRecentMatchesLimit)
            .Select(pin => new
            {
                id = pin.RequestId,
                category = pin.Category,
                categoryIcon = pin.CategoryIcon,
                description = pin.Description,
                createdAt = pin.CreatedAt,
                createdAtIso = pin.CreatedAtIso,
                street = pin.Street,
                city = pin.City,
                latitude = pin.Latitude,
                longitude = pin.Longitude,
                distanceKm = pin.DistanceKm
            });

        return Json(new
        {
            totalMatches = coverageMap.TotalPins,
            activeProposals = myProposals.Count(p => !p.Accepted),
            convertedJobs = myProposals.Count(p => p.Accepted),
            pendingAppointments,
            upcomingConfirmedVisits,
            providerOperationalStatus = profile?.ProviderProfile?.OperationalStatus.ToString() ?? "Online",
            recentMatches,
            coverageMap
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private async Task<ProviderCoverageMapPayload> BuildCoverageMapPayloadAsync(
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = DefaultMapPinPageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPinPage = Math.Max(1, pinPage);
        var normalizedPinPageSize = Math.Clamp(pinPageSize, MinMapPinPageSize, MaxMapPinPageSize);
        var normalizedCategoryFilter = NormalizeFilterValue(categoryFilter);

        var (coverageMap, errorMessage) = await _backendApiClient.GetCoverageMapAsync(
            string.IsNullOrWhiteSpace(normalizedCategoryFilter) ? null : normalizedCategoryFilter,
            maxDistanceKm,
            normalizedPinPage,
            normalizedPinPageSize,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            _logger.LogWarning("Falha ao consultar mapa de cobertura via API: {ErrorMessage}", errorMessage);
        }

        if (coverageMap == null)
        {
            return new ProviderCoverageMapPayload(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                maxDistanceKm,
                normalizedPinPage,
                normalizedPinPageSize,
                0,
                false,
                Array.Empty<ProviderCoverageMapPin>());
        }

        return new ProviderCoverageMapPayload(
            coverageMap.HasBaseLocation,
            coverageMap.ProviderLatitude,
            coverageMap.ProviderLongitude,
            coverageMap.InterestRadiusKm,
            coverageMap.MapSearchRadiusKm,
            coverageMap.BaseZipCode,
            coverageMap.AppliedCategoryFilter,
            coverageMap.AppliedMaxDistanceKm,
            coverageMap.PinPage,
            coverageMap.PinPageSize,
            coverageMap.TotalPins,
            coverageMap.HasMorePins,
            coverageMap.Pins.Select(pin => new ProviderCoverageMapPin(
                    pin.RequestId,
                    pin.Category,
                    pin.CategoryIcon,
                    pin.Description,
                    pin.Street,
                    pin.City,
                    pin.Zip,
                    pin.CreatedAtUtc.ToString("dd/MM HH:mm"),
                    pin.CreatedAtUtc.ToString("O"),
                    pin.Latitude,
                    pin.Longitude,
                    Math.Round(pin.DistanceKm, 2),
                    pin.IsWithinInterestRadius,
                    pin.IsCategoryMatch))
                .ToList());
    }

    private static string NormalizeFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record ProviderCoverageMapPayload(
        bool HasBaseLocation,
        double? ProviderLatitude,
        double? ProviderLongitude,
        double? InterestRadiusKm,
        double? MapSearchRadiusKm,
        string? BaseZipCode,
        string? AppliedCategoryFilter,
        double? AppliedMaxDistanceKm,
        int PinPage,
        int PinPageSize,
        int TotalPins,
        bool HasMorePins,
        IReadOnlyCollection<ProviderCoverageMapPin> Pins);

    private sealed record ProviderCoverageMapPin(
        Guid RequestId,
        string Category,
        string? CategoryIcon,
        string Description,
        string Street,
        string City,
        string Zip,
        string CreatedAt,
        string CreatedAtIso,
        double Latitude,
        double Longitude,
        double DistanceKm,
        bool IsWithinInterestRadius,
        bool IsCategoryMatch);

    private static string ResolveProviderAvatarUrl(UserProfileDto? profile)
    {
        if (!string.IsNullOrWhiteSpace(profile?.ProfilePictureUrl))
        {
            return profile.ProfilePictureUrl;
        }

        var name = string.IsNullOrWhiteSpace(profile?.Name) ? "Prestador" : profile.Name;
        return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(name)}&background=0D6EFD&color=fff";
    }
}
