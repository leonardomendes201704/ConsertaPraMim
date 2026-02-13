using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IProviderOnboardingService _onboardingService;

    public AccountController(IAuthService authService, IProviderOnboardingService onboardingService)
    {
        _authService = authService;
        _onboardingService = onboardingService;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdRaw, out var userId))
            {
                var isOnboardingComplete = await _onboardingService.IsOnboardingCompleteAsync(userId);
                if (!isOnboardingComplete)
                {
                    return RedirectToAction("Index", "Onboarding");
                }
            }

            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var response = await _authService.LoginAsync(new LoginRequest(email, password));

        if (response != null && response.Role == UserRole.Provider.ToString())
        {
            await SignInAsync(response);

            var isOnboardingComplete = await _onboardingService.IsOnboardingCompleteAsync(response.UserId);
            if (!isOnboardingComplete)
            {
                return RedirectToAction("Index", "Onboarding");
            }

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Email ou senha invalidos, ou voce nao tem permissao de prestador.";
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string name, string email, string password, string phone)
    {
        var request = new RegisterRequest(name, email, password, phone, (int)UserRole.Provider);
        var response = await _authService.RegisterAsync(request);

        if (response != null)
        {
            await SignInAsync(response);
            return RedirectToAction("Index", "Onboarding");
        }

        ViewBag.Error = "Erro ao cadastrar. O e-mail pode ja estar em uso.";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private async Task SignInAsync(LoginResponse response)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, response.UserName),
            new Claim(ClaimTypes.Email, response.Email),
            new Claim(ClaimTypes.Role, response.Role),
            new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
            new Claim(WebProviderClaimTypes.ApiToken, response.Token)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));
    }
}
