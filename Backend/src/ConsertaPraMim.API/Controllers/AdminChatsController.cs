using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/chats")]
public class AdminChatsController : ControllerBase
{
    private readonly IAdminChatNotificationService _adminChatNotificationService;

    public AdminChatsController(IAdminChatNotificationService adminChatNotificationService)
    {
        _adminChatNotificationService = adminChatNotificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? requestId,
        [FromQuery] Guid? providerId,
        [FromQuery] Guid? clientId,
        [FromQuery] string? searchTerm,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminChatsQueryDto(requestId, providerId, clientId, searchTerm, fromUtc, toUtc, page, pageSize);
        var response = await _adminChatNotificationService.GetChatsAsync(query);
        return Ok(response);
    }

    [HttpGet("{requestId:guid}/{providerId:guid}")]
    public async Task<IActionResult> GetByRequestAndProvider(Guid requestId, Guid providerId)
    {
        var response = await _adminChatNotificationService.GetChatAsync(requestId, providerId);
        return response == null ? NotFound() : Ok(response);
    }
}
