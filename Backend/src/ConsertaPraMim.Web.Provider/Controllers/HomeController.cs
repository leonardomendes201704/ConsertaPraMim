using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Models;
using System.Linq;
using System.Text.Json;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;
    private readonly IProfileService _profileService;

    public HomeController(
        ILogger<HomeController> logger,
        IServiceRequestService requestService,
        IProposalService proposalService,
        IProfileService profileService)
    {
        _logger = logger;
        _requestService = requestService;
        _proposalService = proposalService;
        _profileService = profileService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        var profile = await _profileService.GetProfileAsync(userId);
        var providerProfile = profile?.ProviderProfile;
        
        // Get available matches
        var matches = await _requestService.GetAllAsync(userId, "Provider");
        var myProposals = await _proposalService.GetByProviderAsync(userId);
        var history = await _requestService.GetHistoryByProviderAsync(userId);

        ViewBag.TotalMatches = matches.Count();
        ViewBag.ActiveProposals = myProposals.Count(p => !p.Accepted);
        ViewBag.ConvertedJobs = myProposals.Count(p => p.Accepted);
        
        // Finance
        ViewBag.TotalRevenue = history.Sum(h => h.EstimatedValue ?? 0);
        ViewBag.AverageTicket = history.Any() ? history.Average(h => (double)(h.EstimatedValue ?? 0)) : 0;
        ViewBag.ProviderOperationalStatus = providerProfile?.OperationalStatus.ToString() ?? "Online";
        ViewBag.ProviderAvatarUrl = ResolveProviderAvatarUrl(profile);
        var coverageMap = await BuildCoverageMapPayloadAsync(userId, providerProfile);
        ViewBag.ProviderCoverageMapJson = JsonSerializer.Serialize(coverageMap);

        return View(matches.Take(5)); // Show recent top 5 matches
    }

    [HttpGet]
    public async Task<IActionResult> RecentMatchesData()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var profile = await _profileService.GetProfileAsync(userId);
        var matches = (await _requestService.GetAllAsync(userId, "Provider")).ToList();
        var myProposals = (await _proposalService.GetByProviderAsync(userId)).ToList();

        var recentMatches = matches.Take(5).Select(r => new
        {
            id = r.Id,
            category = r.Category,
            description = r.Description,
            createdAt = r.CreatedAt.ToString("dd/MM HH:mm"),
            street = r.Street,
            city = r.City
        });

        return Json(new
        {
            totalMatches = matches.Count,
            activeProposals = myProposals.Count(p => !p.Accepted),
            convertedJobs = myProposals.Count(p => p.Accepted),
            providerOperationalStatus = profile?.ProviderProfile?.OperationalStatus.ToString() ?? "Online",
            recentMatches,
            coverageMap = await BuildCoverageMapPayloadAsync(userId, profile?.ProviderProfile)
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

    private async Task<ProviderCoverageMapPayload> BuildCoverageMapPayloadAsync(Guid providerId, ProviderProfileDto? providerProfile)
    {
        if (providerProfile?.BaseLatitude is not double providerLat || providerProfile.BaseLongitude is not double providerLng)
        {
            return new ProviderCoverageMapPayload(
                false,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<ProviderCoverageMapPin>());
        }

        var interestRadiusKm = providerProfile.RadiusKm > 0 ? providerProfile.RadiusKm : 5.0;
        var mapSearchRadiusKm = Math.Clamp(interestRadiusKm * 4, 40.0, 250.0);
        var mapPins = await _requestService.GetMapPinsForProviderAsync(providerId, mapSearchRadiusKm, 250);

        return new ProviderCoverageMapPayload(
            true,
            providerLat,
            providerLng,
            interestRadiusKm,
            mapSearchRadiusKm,
            providerProfile.BaseZipCode,
            mapPins.Select(pin => new ProviderCoverageMapPin(
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

    private sealed record ProviderCoverageMapPayload(
        bool HasBaseLocation,
        double? ProviderLatitude,
        double? ProviderLongitude,
        double? InterestRadiusKm,
        double? MapSearchRadiusKm,
        string? BaseZipCode,
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
