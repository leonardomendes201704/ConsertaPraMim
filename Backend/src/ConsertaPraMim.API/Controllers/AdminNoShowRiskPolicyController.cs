using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/service-appointments/no-show-risk-policy")]
public class AdminNoShowRiskPolicyController : ControllerBase
{
    private readonly IAdminNoShowRiskPolicyService _adminNoShowRiskPolicyService;

    public AdminNoShowRiskPolicyController(IAdminNoShowRiskPolicyService adminNoShowRiskPolicyService)
    {
        _adminNoShowRiskPolicyService = adminNoShowRiskPolicyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var policy = await _adminNoShowRiskPolicyService.GetActiveAsync();
        if (policy == null)
        {
            return NotFound(new AdminNoShowRiskPolicyUpdateResultDto(
                false,
                null,
                "not_found",
                "Politica ativa de risco de no-show nao encontrada."));
        }

        return Ok(policy);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateActive([FromBody] AdminUpdateNoShowRiskPolicyRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminNoShowRiskPolicyService.UpdateActiveAsync(request, actorUserId, actorEmail);
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

    private bool TryGetActor(out Guid actorUserId, out string actorEmail)
    {
        actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        actorUserId = default;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }
}
