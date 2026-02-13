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
    private readonly IZipGeocodingService _zipGeocodingService;

    public ServiceRequestsController(
        IServiceRequestService requestService,
        IProposalService proposalService,
        IZipGeocodingService zipGeocodingService)
    {
        _requestService = requestService;
        _proposalService = proposalService;
        _zipGeocodingService = zipGeocodingService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        var requests = await _requestService.GetAllAsync(userId, "Client");
        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> ListData()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var requests = (await _requestService.GetAllAsync(userId, "Client"))
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

        return Json(new { requests });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ResolveZip(string zipCode)
    {
        var resolved = await _zipGeocodingService.ResolveCoordinatesAsync(zipCode);
        if (!resolved.HasValue)
        {
            return NotFound(new { message = "Nao foi possivel localizar esse CEP." });
        }

        return Json(new
        {
            zipCode = resolved.Value.NormalizedZip,
            street = resolved.Value.Street,
            city = resolved.Value.City
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);

        Guid requestId;
        try
        {
            requestId = await _requestService.CreateAsync(userId, dto);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(dto.Zip), ex.Message);
            return View(dto);
        }
        
        TempData["Success"] = "Pedido criado com sucesso! Aguarde propostas profissionais.";
        return RedirectToAction("Details", new { id = requestId });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, UserRole.Client.ToString());
        if (request == null) return NotFound();

        var proposals = await _proposalService.GetByRequestAsync(id, userId, UserRole.Client.ToString());
        ViewBag.Proposals = proposals;

        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsData(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, UserRole.Client.ToString());
        if (request == null) return NotFound();

        var proposals = await _proposalService.GetByRequestAsync(id, userId, UserRole.Client.ToString());
        return Json(new
        {
            requestStatus = request.Status,
            proposals = proposals
        });
    }
}
