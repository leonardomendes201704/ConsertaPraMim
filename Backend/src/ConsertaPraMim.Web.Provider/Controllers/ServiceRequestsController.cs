using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;

    public ServiceRequestsController(IServiceRequestService requestService, IProposalService proposalService)
    {
        _requestService = requestService;
        _proposalService = proposalService;
    }

    public async Task<IActionResult> Index(string? searchTerm)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        var matches = await _requestService.GetAllAsync(userId, "Provider", searchTerm);

        ViewBag.SearchTerm = searchTerm;
        return View(matches);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var request = await _requestService.GetByIdAsync(id);
        if (request == null) return NotFound();

        // Check if I already sent a proposal
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        
        var myProposals = await _proposalService.GetByProviderAsync(userId);
        var existingProposal = myProposals.FirstOrDefault(p => p.RequestId == id);

        ViewBag.ExistingProposal = existingProposal;

        return View(request);
    }

    public async Task<IActionResult> Agenda()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var scheduled = await _requestService.GetScheduledByProviderAsync(userId);
        return View(scheduled);
    }

    public async Task<IActionResult> History()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var history = await _requestService.GetHistoryByProviderAsync(userId);
        return View(history);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var success = await _requestService.CompleteAsync(id, userId);
        if (success)
        {
            TempData["Success"] = "Serviço marcado como concluído com sucesso!";
        }
        else
        {
            TempData["Error"] = "Não foi possível concluir o serviço.";
        }

        return RedirectToAction("Agenda");
    }
}
