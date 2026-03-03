using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.ErrorMessage = "A integracao de autenticacao sera conectada a API na proxima task da ST-005.";
        model.Password = string.Empty;
        return View(model);
    }
}
