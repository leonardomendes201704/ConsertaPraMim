using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/proposals")]
public class AdminProposalsController : ControllerBase
{
    private readonly IAdminRequestProposalService _adminRequestProposalService;

    public AdminProposalsController(IAdminRequestProposalService adminRequestProposalService)
    {
        _adminRequestProposalService = adminRequestProposalService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? requestId,
        [FromQuery] Guid? providerId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminProposalsQueryDto(requestId, providerId, status, fromUtc, toUtc, page, pageSize);
        var response = await _adminRequestProposalService.GetProposalsAsync(query);
        return Ok(response);
    }

    [HttpPut("{id:guid}/invalidate")]
    public async Task<IActionResult> Invalidate(Guid id, [FromBody] AdminInvalidateProposalRequestDto request)
    {
        var actorUserIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUserIdRaw) || !Guid.TryParse(actorUserIdRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _adminRequestProposalService.InvalidateProposalAsync(id, request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            _ => BadRequest(result)
        };
    }
}
