using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Web.Client.Services;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class HomeController : Controller
{
    private readonly IClientDashboardApiClient _dashboardApiClient;

    public HomeController(IClientDashboardApiClient dashboardApiClient)
    {
        _dashboardApiClient = dashboardApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var (requests, errorMessage) = await _dashboardApiClient.GetMyRequestsAsync(HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            ViewBag.Error = errorMessage;
        }
        
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

        var (requestsFromApi, errorMessage) = await _dashboardApiClient.GetMyRequestsAsync(HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return BadRequest(new { message = errorMessage });
        }

        var requests = requestsFromApi.ToList();

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
