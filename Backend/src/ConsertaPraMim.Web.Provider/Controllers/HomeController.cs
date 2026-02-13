using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Provider.Models;
using System.Linq;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;

    public HomeController(ILogger<HomeController> logger, IServiceRequestService requestService, IProposalService proposalService)
    {
        _logger = logger;
        _requestService = requestService;
        _proposalService = proposalService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        
        // Get available matches
        var matches = await _requestService.GetAllAsync(userId, "Provider");
        var myProposals = await _proposalService.GetByProviderAsync(userId);
        var history = await _requestService.GetHistoryByProviderAsync(userId);

        ViewBag.TotalMatches = matches.Count();
        ViewBag.ActiveProposals = myProposals.Count(p => !p.Accepted);
        ViewBag.ConvertedJobs = myProposals.Count(p => p.Accepted);
        
        // Finance
        ViewBag.TotalRevenue = history.Sum(h => h.EstimatedValue ?? 0);
        ViewBag.AverageTicket = history.Any() ? history.Average(h => (double)(h.EstimatedValue ?? 0)) : 0;

        return View(matches.Take(5)); // Show recent top 5 matches
    }

    [HttpGet]
    public async Task<IActionResult> RecentMatchesData()
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var matches = (await _requestService.GetAllAsync(userId, "Provider")).ToList();
        var myProposals = (await _proposalService.GetByProviderAsync(userId)).ToList();

        var recentMatches = matches.Take(5).Select(r => new
        {
            id = r.Id,
            category = r.Category,
            description = r.Description,
            createdAt = r.CreatedAt.ToString("dd/MM HH:mm"),
            street = r.Street,
            city = r.City
        });

        return Json(new
        {
            totalMatches = matches.Count,
            activeProposals = myProposals.Count(p => !p.Accepted),
            convertedJobs = myProposals.Count(p => p.Accepted),
            recentMatches
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
