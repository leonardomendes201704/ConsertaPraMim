using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/service-categories")]
public class AdminServiceCategoriesController : ControllerBase
{
    private readonly IAdminServiceCategoryService _adminServiceCategoryService;

    public AdminServiceCategoriesController(IAdminServiceCategoryService adminServiceCategoryService)
    {
        _adminServiceCategoryService = adminServiceCategoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true)
    {
        var categories = await _adminServiceCategoryService.GetAllAsync(includeInactive);
        return Ok(categories);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateServiceCategoryRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminServiceCategoryService.CreateAsync(request, actorUserId, actorEmail);
        if (result.Success && result.Category != null)
        {
            return CreatedAtAction(nameof(GetAll), new { includeInactive = true }, result);
        }

        return result.ErrorCode switch
        {
            "duplicate_name" => Conflict(result),
            "duplicate_slug" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminUpdateServiceCategoryRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminServiceCategoryService.UpdateAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "duplicate_name" => Conflict(result),
            "duplicate_slug" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateServiceCategoryStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminServiceCategoryService.UpdateStatusAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "last_active_forbidden" => Conflict(result),
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
