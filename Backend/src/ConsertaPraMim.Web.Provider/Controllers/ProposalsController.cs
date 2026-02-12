using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProposalsController : Controller
{
    private readonly IProposalService _proposalService;

    public ProposalsController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        
        var proposals = await _proposalService.GetByProviderAsync(userId);
        return View(proposals);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid requestId, decimal? estimatedValue, string? message)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);
        
        var dto = new CreateProposalDto(requestId, estimatedValue, message);
        await _proposalService.CreateAsync(userId, dto);

        TempData["Success"] = "Proposta enviada com sucesso! Aguarde o retorno do cliente.";
        return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
    }
}
