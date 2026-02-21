using System.Security.Claims;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await _profileService.GetProfileAsync(userId);
        if (profile == null)
        {
            TempData["Error"] = "Nao foi possivel carregar seu perfil.";
            return RedirectToAction("Index", "Home");
        }

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePicture(string imageUrl)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            TempData["Error"] = "Informe uma URL de imagem valida.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _profileService.UpdateProfilePictureAsync(userId, imageUrl);
        TempData[success ? "Success" : "Error"] = success
            ? "Foto de perfil atualizada com sucesso."
            : "Nao foi possivel atualizar a foto de perfil.";

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdRaw, out userId);
    }
}
