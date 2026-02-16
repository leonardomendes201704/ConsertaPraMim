using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
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
    public async Task<IActionResult> Client(Guid clientId)
    {
        if (clientId == Guid.Empty)
        {
            return NotFound();
        }

        var profile = await _profileService.GetProfileAsync(clientId);
        if (profile == null ||
            !string.Equals(profile.Role, UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var reputation = await _reviewService.GetClientScoreSummaryAsync(clientId);
        var recentReviews = (await _reviewService.GetByClientAsync(clientId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToList();

        var viewModel = new ClientPublicProfileViewModel(
            clientId,
            profile,
            reputation,
            recentReviews);

        return View(viewModel);
    }
}
