using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminManualController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
