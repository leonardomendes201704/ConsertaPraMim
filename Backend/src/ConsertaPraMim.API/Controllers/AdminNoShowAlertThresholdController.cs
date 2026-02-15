using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/no-show-alert-thresholds")]
public class AdminNoShowAlertThresholdController : ControllerBase
{
    private readonly IAdminNoShowAlertThresholdService _adminNoShowAlertThresholdService;

    public AdminNoShowAlertThresholdController(IAdminNoShowAlertThresholdService adminNoShowAlertThresholdService)
    {
        _adminNoShowAlertThresholdService = adminNoShowAlertThresholdService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var configuration = await _adminNoShowAlertThresholdService.GetActiveAsync();
        if (configuration == null)
        {
            return NotFound(new AdminNoShowAlertThresholdUpdateResultDto(
                false,
                null,
                "not_found",
                "Configuracao ativa de threshold de no-show nao encontrada."));
        }

        return Ok(configuration);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateActive([FromBody] AdminUpdateNoShowAlertThresholdRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminNoShowAlertThresholdService.UpdateActiveAsync(request, actorUserId, actorEmail);
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
