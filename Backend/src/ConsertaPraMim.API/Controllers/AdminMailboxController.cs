using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/mailbox")]
public class AdminMailboxController : ControllerBase
{
    private readonly IAdminMailboxService _adminMailboxService;

    public AdminMailboxController(IAdminMailboxService adminMailboxService)
    {
        _adminMailboxService = adminMailboxService;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _adminMailboxService.GetSettingsAsync(HttpContext.RequestAborted);
        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpsertSettings([FromBody] AdminMailboxUpsertSettingsRequestDto request)
    {
        var actorUserId = ResolveActorUserId();
        var actorEmail = ResolveActorEmail();
        var result = await _adminMailboxService.UpsertSettingsAsync(
            request,
            actorUserId,
            actorEmail,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = result.ErrorCode,
                errorMessage = result.ErrorMessage
            });
        }

        return Ok(new { success = true });
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients(
        [FromQuery] string? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 50)
    {
        var recipients = await _adminMailboxService.GetRecipientsAsync(
            role,
            search,
            take,
            HttpContext.RequestAborted);
        return Ok(recipients);
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? folder = "inbox",
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        var result = await _adminMailboxService.GetMessagesAsync(
            new AdminMailboxListQueryDto(folder, search, page, pageSize),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("messages/{messageId}")]
    public async Task<IActionResult> GetMessage([FromRoute] string messageId)
    {
        var result = await _adminMailboxService.GetMessageByIdAsync(messageId, HttpContext.RequestAborted);
        if (!result.Success || result.Message == null)
        {
            return NotFound(new
            {
                success = false,
                errorCode = result.ErrorCode ?? "admin_mailbox_message_not_found",
                errorMessage = result.ErrorMessage ?? "Email nao encontrado."
            });
        }

        return Ok(result.Message);
    }

    [HttpPatch("messages/{messageId}/read")]
    public async Task<IActionResult> MarkRead([FromRoute] string messageId, [FromBody] AdminMailboxMarkReadRequestDto request)
    {
        var result = await _adminMailboxService.MarkMessageReadAsync(
            messageId,
            request.IsRead,
            HttpContext.RequestAborted);
        if (!result.Success || result.Message == null)
        {
            return NotFound(new
            {
                success = false,
                errorCode = result.ErrorCode ?? "admin_mailbox_message_not_found",
                errorMessage = result.ErrorMessage ?? "Email nao encontrado."
            });
        }

        return Ok(result.Message);
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] AdminMailboxSendRequestDto request)
    {
        var actorUserId = ResolveActorUserId();
        var actorEmail = ResolveActorEmail();
        var result = await _adminMailboxService.SendAsync(
            request,
            actorUserId,
            actorEmail,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = result.ErrorCode,
                errorMessage = result.ErrorMessage
            });
        }

        return Ok(new
        {
            success = true,
            message = result.Message
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var result = await _adminMailboxService.SyncInboxAsync(
            notifyAdmins: true,
            cancellationToken: HttpContext.RequestAborted);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = result.ErrorCode,
                errorMessage = result.ErrorMessage
            });
        }

        return Ok(result);
    }

    private Guid ResolveActorUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.Empty;
    }

    private string? ResolveActorEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ??
               User.Identity?.Name;
    }
}
