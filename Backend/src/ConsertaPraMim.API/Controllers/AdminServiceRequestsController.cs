using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/service-requests")]
public class AdminServiceRequestsController : ControllerBase
{
    private readonly IAdminRequestProposalService _adminRequestProposalService;

    public AdminServiceRequestsController(IAdminRequestProposalService adminRequestProposalService)
    {
        _adminRequestProposalService = adminRequestProposalService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminServiceRequestsQueryDto(searchTerm, status, category, fromUtc, toUtc, page, pageSize);
        var response = await _adminRequestProposalService.GetServiceRequestsAsync(query);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var request = await _adminRequestProposalService.GetServiceRequestByIdAsync(id);
        return request == null ? NotFound() : Ok(request);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminUpdateServiceRequestStatusRequestDto request)
    {
        var actorUserIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUserIdRaw) || !Guid.TryParse(actorUserIdRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _adminRequestProposalService.UpdateServiceRequestStatusAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "invalid_status" => BadRequest(result),
            _ => BadRequest(result)
        };
    }
}
