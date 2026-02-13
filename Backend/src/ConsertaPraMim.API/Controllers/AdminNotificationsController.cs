using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly IAdminChatNotificationService _adminChatNotificationService;

    public AdminNotificationsController(IAdminChatNotificationService adminChatNotificationService)
    {
        _adminChatNotificationService = adminChatNotificationService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] AdminSendNotificationRequestDto request)
    {
        var actorUserIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorUserIdRaw) || !Guid.TryParse(actorUserIdRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _adminChatNotificationService.SendNotificationAsync(request, actorUserId, actorEmail);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "recipient_inactive" => Conflict(result),
            _ => BadRequest(result)
        };
    }
}
