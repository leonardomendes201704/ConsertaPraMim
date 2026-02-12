using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestService _requestService;
    private readonly IProposalService _proposalService;

    public ServiceRequestsController(IServiceRequestService requestService, IProposalService proposalService)
    {
        _requestService = requestService;
        _proposalService = proposalService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        var requests = await _requestService.GetAllAsync(userId, "Client");
        return View(requests);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        var requestId = await _requestService.CreateAsync(userId, dto);
        
        TempData["Success"] = "Pedido criado com sucesso! Aguarde propostas profissionais.";
        return RedirectToAction("Details", new { id = requestId });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var request = await _requestService.GetByIdAsync(id);
        if (request == null) return NotFound();

        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        // Security check
        // Check if the request belongs to this client (Assuming repo handles this or we check here)
        // Add check if needed.

        var proposals = await _proposalService.GetByRequestAsync(id);
        ViewBag.Proposals = proposals;

        return View(request);
    }
}
