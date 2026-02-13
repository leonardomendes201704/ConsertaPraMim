using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/chat-attachments")]
public class AdminChatAttachmentsController : ControllerBase
{
    private readonly IAdminChatNotificationService _adminChatNotificationService;

    public AdminChatAttachmentsController(IAdminChatNotificationService adminChatNotificationService)
    {
        _adminChatNotificationService = adminChatNotificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? requestId,
        [FromQuery] Guid? userId,
        [FromQuery] string? mediaKind,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminChatAttachmentsQueryDto(
            requestId,
            userId,
            mediaKind,
            fromUtc,
            toUtc,
            page,
            pageSize);

        var response = await _adminChatNotificationService.GetChatAttachmentsAsync(query);
        return Ok(response);
    }
}
