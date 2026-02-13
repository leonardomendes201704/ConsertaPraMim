using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminUsersQueryDto(searchTerm, role, isActive, page, pageSize);
        var response = await _adminUserService.GetUsersAsync(query);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _adminUserService.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateUserStatusRequestDto request)
    {
        var actorUserIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUserIdRaw) || !Guid.TryParse(actorUserIdRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _adminUserService.UpdateStatusAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "self_deactivate_forbidden" => Conflict(result),
            "last_admin_forbidden" => Conflict(result),
            _ => BadRequest(result)
        };
    }
}
