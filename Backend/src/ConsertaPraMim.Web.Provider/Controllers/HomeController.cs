using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Models;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using System.Text;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class HomeController : Controller
{
    private const int DashboardRecentMatchesLimit = 15;
    private const int DefaultMapPinPageSize = 120;
    private const int MinMapPinPageSize = 20;
    private const int MaxMapPinPageSize = 200;
    private const int MaxMapPinTake = 500;

    private readonly ILogger<HomeController> _logger;
    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;
    private readonly IProfileService _profileService;
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public HomeController(
        ILogger<HomeController> logger,
        IServiceRequestService requestService,
        IProposalService proposalService,
        IProfileService profileService,
        IServiceAppointmentService serviceAppointmentService)
    {
        _logger = logger;
        _requestService = requestService;
        _proposalService = proposalService;
        _profileService = profileService;
        _serviceAppointmentService = serviceAppointmentService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        var profile = await _profileService.GetProfileAsync(userId);
        var providerProfile = profile?.ProviderProfile;
        
        // Get available matches
        var matches = (await _requestService.GetAllAsync(userId, "Provider")).ToList();
        var myProposals = await _proposalService.GetByProviderAsync(userId);
        var history = await _requestService.GetHistoryByProviderAsync(userId);
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(userId, "Provider");
        var nowUtc = DateTime.UtcNow;
        var pendingAppointments = appointments.Count(a =>
            string.Equals(a.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase));
        var upcomingConfirmedVisits = appointments.Count(a =>
            (string.Equals(a.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase)) &&
            a.WindowStartUtc >= nowUtc);

        ViewBag.TotalMatches = matches.Count();
        ViewBag.ActiveProposals = myProposals.Count(p => !p.Accepted);
        ViewBag.ConvertedJobs = myProposals.Count(p => p.Accepted);
        ViewBag.PendingAppointments = pendingAppointments;
        ViewBag.UpcomingConfirmedVisits = upcomingConfirmedVisits;
        
        // Finance
        ViewBag.TotalRevenue = history.Sum(h => h.EstimatedValue ?? 0);
        ViewBag.AverageTicket = history.Any() ? history.Average(h => (double)(h.EstimatedValue ?? 0)) : 0;
        ViewBag.ProviderOperationalStatus = providerProfile?.OperationalStatus.ToString() ?? "Online";
        ViewBag.ProviderAvatarUrl = ResolveProviderAvatarUrl(profile);
        ViewBag.RecentMatchesLimit = DashboardRecentMatchesLimit;
        var coverageMap = await BuildCoverageMapPayloadAsync(userId, providerProfile);
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

        var userId = Guid.Parse(userIdString);
        var profile = await _profileService.GetProfileAsync(userId);
        var myProposals = (await _proposalService.GetByProviderAsync(userId)).ToList();
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(userId, "Provider");
        var coverageMap = await BuildCoverageMapPayloadAsync(
            userId,
            profile?.ProviderProfile,
            category,
            maxDistanceKm,
            pinPage,
            pinPageSize);
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
                description = pin.Description,
                createdAt = pin.CreatedAt,
                createdAtIso = pin.CreatedAtIso,
                street = pin.Street,
                city = pin.City,
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
        Guid providerId,
        ProviderProfileDto? providerProfile,
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = DefaultMapPinPageSize)
    {
        var normalizedPinPage = Math.Max(1, pinPage);
        var normalizedPinPageSize = Math.Clamp(pinPageSize, MinMapPinPageSize, MaxMapPinPageSize);

        if (providerProfile?.BaseLatitude is not double providerLat || providerProfile.BaseLongitude is not double providerLng)
        {
            return new ProviderCoverageMapPayload(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                normalizedPinPage,
                normalizedPinPageSize,
                0,
                false,
                Array.Empty<ProviderCoverageMapPin>());
        }

        var interestRadiusKm = providerProfile.RadiusKm > 0 ? providerProfile.RadiusKm : 5.0;
        var defaultMapSearchRadiusKm = Math.Clamp(interestRadiusKm * 4, 40.0, 250.0);
        var requestedDistanceKm = maxDistanceKm.HasValue && maxDistanceKm.Value > 0
            ? Math.Min(maxDistanceKm.Value, defaultMapSearchRadiusKm)
            : defaultMapSearchRadiusKm;
        var normalizedCategoryFilter = NormalizeFilterValue(categoryFilter);
        var takeForLookup = Math.Clamp(normalizedPinPage * normalizedPinPageSize, normalizedPinPageSize, MaxMapPinTake);
        var mapPins = await _requestService.GetMapPinsForProviderAsync(providerId, requestedDistanceKm, takeForLookup);

        if (string.IsNullOrWhiteSpace(normalizedCategoryFilter))
        {
            normalizedCategoryFilter = string.Empty;
        }
        else
        {
            mapPins = mapPins.Where(pin =>
                string.Equals(
                    NormalizeFilterValue(pin.Category),
                    normalizedCategoryFilter,
                    StringComparison.Ordinal));
        }

        var filteredPins = mapPins.ToList();
        var filteredTotal = filteredPins.Count;
        var skip = (normalizedPinPage - 1) * normalizedPinPageSize;
        var pagedPins = skip >= filteredTotal
            ? new List<ProviderServiceMapPinDto>()
            : filteredPins.Skip(skip).Take(normalizedPinPageSize).ToList();
        var hasMorePins = skip + pagedPins.Count < filteredTotal;

        return new ProviderCoverageMapPayload(
            true,
            providerLat,
            providerLng,
            interestRadiusKm,
            requestedDistanceKm,
            providerProfile.BaseZipCode,
            string.IsNullOrWhiteSpace(normalizedCategoryFilter) ? null : normalizedCategoryFilter,
            maxDistanceKm,
            normalizedPinPage,
            normalizedPinPageSize,
            filteredTotal,
            hasMorePins,
            pagedPins.Select(pin => new ProviderCoverageMapPin(
                pin.RequestId,
                pin.Category,
                pin.Description,
                pin.Street,
                pin.City,
                pin.Zip,
                pin.CreatedAt.ToString("dd/MM HH:mm"),
                pin.CreatedAt.ToString("O"),
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
