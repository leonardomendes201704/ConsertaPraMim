using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/support/tickets")]
public class AdminSupportTicketsController : ControllerBase
{
    private const long SupportAttachmentMaxFileSizeBytes = 25_000_000;

    private static readonly HashSet<string> AllowedSupportAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
        ".mp4", ".webm", ".mov", ".avi",
        ".mp3", ".wav", ".ogg", ".m4a", ".aac",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip"
    };

    private readonly IAdminSupportTicketService _adminSupportTicketService;
    private readonly IFileStorageService _fileStorageService;

    public AdminSupportTicketsController(
        IAdminSupportTicketService adminSupportTicketService,
        IFileStorageService fileStorageService)
    {
        _adminSupportTicketService = adminSupportTicketService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminSupportTicketListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? assignedAdminUserId = null,
        [FromQuery] bool? assignedOnly = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int firstResponseSlaMinutes = 60)
    {
        var response = await _adminSupportTicketService.GetTicketsAsync(
            new AdminSupportTicketListQueryDto(
                status,
                priority,
                assignedAdminUserId,
                assignedOnly,
                search,
                sortBy,
                sortDescending,
                page,
                pageSize,
                firstResponseSlaMinutes));

        return Ok(response);
    }

    [HttpGet("{ticketId:guid}")]
    [ProducesResponseType(typeof(AdminSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketDetails([FromRoute] Guid ticketId)
    {
        var result = await _adminSupportTicketService.GetTicketDetailsAsync(ticketId);
        if (!result.Success || result.Ticket == null)
        {
            return MapFailure(result);
        }

        return Ok(result.Ticket);
    }

    [HttpPost("{ticketId:guid}/messages")]
    [ProducesResponseType(typeof(AdminSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMessage(
        [FromRoute] Guid ticketId,
        [FromBody] AdminSupportTicketMessageRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminSupportTicketService.AddMessageAsync(ticketId, actorUserId, actorEmail, request);
        if (!result.Success || result.Ticket == null)
        {
            return MapFailure(result);
        }

        return Ok(result.Ticket);
    }

    [HttpPost("{ticketId:guid}/attachments/upload")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAttachment([FromRoute] Guid ticketId, [FromForm] IFormFile? file)
    {
        if (ticketId == Guid.Empty)
        {
            return BadRequest(new
            {
                errorCode = "admin_support_invalid_ticket",
                message = "Chamado invalido."
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                errorCode = "admin_support_attachment_required",
                message = "Arquivo obrigatorio."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedSupportAttachmentExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                errorCode = "admin_support_attachment_unsupported_type",
                message = "Tipo de arquivo nao suportado."
            });
        }

        if (file.Length > SupportAttachmentMaxFileSizeBytes)
        {
            return BadRequest(new
            {
                errorCode = "admin_support_attachment_too_large",
                message = $"Arquivo excede o limite de {SupportAttachmentMaxFileSizeBytes / 1_000_000}MB."
            });
        }

        var details = await _adminSupportTicketService.GetTicketDetailsAsync(ticketId);
        if (!details.Success || details.Ticket == null)
        {
            return MapFailure(details);
        }

        await using var stream = file.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, file.FileName, "support");
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";
        var normalizedContentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType.Trim();

        return Ok(new SupportTicketUploadAttachmentDto(
            absoluteUrl,
            file.FileName,
            normalizedContentType,
            file.Length,
            ResolveSupportMediaKind(normalizedContentType, extension)));
    }

    [HttpPatch("{ticketId:guid}/status")]
    [ProducesResponseType(typeof(AdminSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid ticketId,
        [FromBody] AdminSupportTicketStatusUpdateRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminSupportTicketService.UpdateStatusAsync(ticketId, actorUserId, actorEmail, request);
        if (!result.Success || result.Ticket == null)
        {
            return MapFailure(result);
        }

        return Ok(result.Ticket);
    }

    [HttpPatch("{ticketId:guid}/assign")]
    [ProducesResponseType(typeof(AdminSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        [FromRoute] Guid ticketId,
        [FromBody] AdminSupportTicketAssignRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminSupportTicketService.AssignAsync(ticketId, actorUserId, actorEmail, request);
        if (!result.Success || result.Ticket == null)
        {
            return MapFailure(result);
        }

        return Ok(result.Ticket);
    }

    private bool TryGetActor(out Guid actorUserId, out string actorEmail)
    {
        actorUserId = Guid.Empty;
        actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }

    private IActionResult MapFailure(AdminSupportTicketOperationResultDto result)
    {
        return result.ErrorCode switch
        {
            "admin_support_invalid_actor" or "admin_support_actor_not_found" => Unauthorized(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "admin_support_actor_not_admin" => StatusCode(StatusCodes.Status403Forbidden, new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "admin_support_ticket_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "admin_support_invalid_state" or "admin_support_invalid_transition" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "admin_support_unexpected_error",
                message = result.ErrorMessage ?? "Falha ao processar operacao do chamado."
            })
        };
    }

    private static string ResolveSupportMediaKind(string? contentType, string? extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "image";
            }

            if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return "video";
            }

            if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return "audio";
            }
        }

        return (extension ?? string.Empty).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "image",
            ".mp4" or ".webm" or ".mov" or ".avi" => "video",
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".aac" => "audio",
            _ => "document"
        };
    }
}
