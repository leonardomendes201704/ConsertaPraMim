using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class HomeController : Controller
{
    private readonly IServiceRequestService _requestService;

    public HomeController(IServiceRequestService requestService)
    {
        _requestService = requestService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var requests = await _requestService.GetAllAsync(userId, "Client");
        
        ViewBag.TotalRequests = requests.Count();
        ViewBag.PendingProposals = requests.Count(r => r.Status == "Created" || r.Status == "Matching");
        ViewBag.CompletedPayments = requests.Count(r => r.Status == "Completed" || r.Status == "Validated");

        return View(requests.Take(5));
    }
}
