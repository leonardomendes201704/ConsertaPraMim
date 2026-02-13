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

    [HttpGet]
    public async Task<IActionResult> RecentRequestsData()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var requests = (await _requestService.GetAllAsync(userId, "Client")).ToList();

        var recent = requests
            .Take(5)
            .Select(r => new
            {
                id = r.Id,
                status = r.Status,
                category = r.Category,
                description = r.Description,
                createdAt = r.CreatedAt.ToString("dd/MM/yyyy"),
                street = r.Street,
                city = r.City
            });

        return Json(new
        {
            pendingProposals = requests.Count(r => r.Status == "Created" || r.Status == "Matching"),
            completedPayments = requests.Count(r => r.Status == "Completed" || r.Status == "Validated"),
            recentRequests = recent
        });
    }
}
