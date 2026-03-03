using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
