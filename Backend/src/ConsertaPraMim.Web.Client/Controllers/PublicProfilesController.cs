using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class PublicProfilesController : Controller
{
    private readonly IProfileService _profileService;
    private readonly IReviewService _reviewService;

    public PublicProfilesController(
        IProfileService profileService,
        IReviewService reviewService)
    {
        _profileService = profileService;
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<IActionResult> Provider(Guid providerId)
    {
        if (providerId == Guid.Empty)
        {
            return NotFound();
        }

        var profile = await _profileService.GetProfileAsync(providerId);
        if (profile == null ||
            !string.Equals(profile.Role, UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase) ||
            profile.ProviderProfile == null)
        {
            return NotFound();
        }

        var reputation = await _reviewService.GetProviderScoreSummaryAsync(providerId);
        var recentReviews = (await _reviewService.GetByProviderAsync(providerId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToList();

        var viewModel = new ProviderPublicProfileViewModel(
            providerId,
            profile,
            reputation,
            recentReviews);

        return View(viewModel);
    }
}
