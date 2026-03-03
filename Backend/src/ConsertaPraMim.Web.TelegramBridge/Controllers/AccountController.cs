using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Security;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

public sealed class AccountController : Controller
{
    private readonly ITelegramBridgeAuthApiClient _telegramBridgeAuthApiClient;

    public AccountController(ITelegramBridgeAuthApiClient telegramBridgeAuthApiClient)
    {
        _telegramBridgeAuthApiClient = telegramBridgeAuthApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var hasApiToken = !string.IsNullOrWhiteSpace(User.FindFirst(TelegramBridgeClaimTypes.ApiToken)?.Value);
            if (!hasApiToken)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            else
            {
                return RedirectToLocal(returnUrl);
            }
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginResult = await _telegramBridgeAuthApiClient.LoginAsync(
            model.Email,
            model.Password,
            cancellationToken);

        if (loginResult.Response == null)
        {
            model.ErrorMessage = loginResult.ErrorMessage ?? "Falha ao autenticar com a API.";
            model.Password = string.Empty;
            return View(model);
        }

        if (!loginResult.Response.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            model.ErrorMessage = "A conta autenticada nao possui perfil de cliente.";
            model.Password = string.Empty;
            return View(model);
        }

        await SignInAsync(loginResult.Response, model.RememberMe);
        return RedirectToLocal(model.ReturnUrl);
    }

    private async Task SignInAsync(TelegramBridgeLoginResponse response, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, response.UserName),
            new(ClaimTypes.Email, response.Email),
            new(ClaimTypes.Role, response.Role),
            new(ClaimTypes.NameIdentifier, response.UserId.ToString()),
            new(TelegramBridgeClaimTypes.ApiToken, response.Token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            AllowRefresh = true,
            ExpiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(7)
                : DateTimeOffset.UtcNow.AddHours(12)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
