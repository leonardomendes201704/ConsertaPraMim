using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IReviewService _reviewService;

    public ProfileController(IProfileService profileService, IFileStorageService fileStorageService, IReviewService reviewService)
    {
        _profileService = profileService;
        _fileStorageService = fileStorageService;
        _reviewService = reviewService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        
        var profile = await _profileService.GetProfileAsync(userId);
        ViewBag.Reviews = await _reviewService.GetByProviderAsync(userId);
        return View(profile);
    }

    [HttpPost]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        if (file != null)
        {
            using (var stream = file.OpenReadStream())
            {
                var imageUrl = await _fileStorageService.SaveFileAsync(stream, file.FileName, "profiles");
                await _profileService.UpdateProfilePictureAsync(userId, imageUrl);
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int radiusKm, string categories)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        // Parse categories from comma separated string
        var categoryList = categories.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(c => Enum.Parse<ServiceCategory>(c.Trim()))
                                     .ToList();

        var dto = new UpdateProviderProfileDto(radiusKm, -22.9068, -43.1729, categoryList); // Static coords for MVP
        
        await _profileService.UpdateProviderProfileAsync(userId, dto);
        
        TempData["Success"] = "Perfil atualizado com sucesso!";
        return RedirectToAction("Index");
    }
}
