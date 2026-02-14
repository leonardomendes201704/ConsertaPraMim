using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/checklist-templates")]
public class AdminChecklistTemplatesController : ControllerBase
{
    private readonly IAdminChecklistTemplateService _adminChecklistTemplateService;

    public AdminChecklistTemplatesController(IAdminChecklistTemplateService adminChecklistTemplateService)
    {
        _adminChecklistTemplateService = adminChecklistTemplateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true)
    {
        var templates = await _adminChecklistTemplateService.GetAllAsync(includeInactive);
        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateChecklistTemplateRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminChecklistTemplateService.CreateAsync(request, actorUserId, actorEmail);
        if (result.Success && result.Template != null)
        {
            return CreatedAtAction(nameof(GetAll), new { includeInactive = true }, result);
        }

        return result.ErrorCode switch
        {
            "category_not_found" => NotFound(result),
            "template_already_exists" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUpdateChecklistTemplateRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminChecklistTemplateService.UpdateAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "template_not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateChecklistTemplateStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminChecklistTemplateService.UpdateStatusAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "template_not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    private bool TryGetActor(out Guid actorUserId, out string actorEmail)
    {
        actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        actorUserId = default;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }
}
