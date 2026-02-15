using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/service-appointments")]
public class ServiceAppointmentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedScopeAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IFileStorageService _fileStorageService;

    public ServiceAppointmentsController(
        IServiceAppointmentService serviceAppointmentService,
        IServiceAppointmentChecklistService serviceAppointmentChecklistService,
        IFileStorageService fileStorageService)
    {
        _serviceAppointmentService = serviceAppointmentService;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots([FromQuery] GetServiceAppointmentSlotsQueryDto query)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetAvailableSlotsAsync(actorUserId, actorRole, query);
        if (result.Success)
        {
            return Ok(result.Slots);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("providers/{providerId:guid}/availability")]
    public async Task<IActionResult> GetProviderAvailability(Guid providerId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetProviderAvailabilityOverviewAsync(actorUserId, actorRole, providerId);
        if (result.Success && result.Overview != null)
        {
            return Ok(result.Overview);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("availability/rules")]
    public async Task<IActionResult> AddProviderAvailabilityRule([FromBody] CreateProviderAvailabilityRuleRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityRuleAsync(actorUserId, actorRole, request);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpDelete("availability/rules/{ruleId:guid}")]
    public async Task<IActionResult> RemoveProviderAvailabilityRule(Guid ruleId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityRuleAsync(actorUserId, actorRole, ruleId);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("availability/blocks")]
    public async Task<IActionResult> AddProviderAvailabilityBlock([FromBody] CreateProviderAvailabilityExceptionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityExceptionAsync(actorUserId, actorRole, request);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpDelete("availability/blocks/{exceptionId:guid}")]
    public async Task<IActionResult> RemoveProviderAvailabilityBlock(Guid exceptionId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityExceptionAsync(actorUserId, actorRole, exceptionId);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CreateAsync(actorUserId, actorRole, request);
        if (result.Success && result.Appointment != null)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Appointment.Id }, result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ConfirmAsync(actorUserId, actorRole, id);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RejectAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> RequestReschedule(Guid id, [FromBody] RequestServiceAppointmentRescheduleDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RequestRescheduleAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reschedule/respond")]
    public async Task<IActionResult> RespondReschedule(Guid id, [FromBody] RespondServiceAppointmentRescheduleRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RespondRescheduleAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CancelAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/arrive")]
    public async Task<IActionResult> MarkArrived(Guid id, [FromBody] MarkServiceAppointmentArrivalRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.MarkArrivedAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartExecution(Guid id, [FromBody] StartServiceAppointmentExecutionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.StartExecutionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/presence/respond")]
    public async Task<IActionResult> RespondPresence(Guid id, [FromBody] RespondServiceAppointmentPresenceRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RespondPresenceAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/operational-status")]
    public async Task<IActionResult> UpdateOperationalStatus(Guid id, [FromBody] UpdateServiceAppointmentOperationalStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.UpdateOperationalStatusAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes")]
    public async Task<IActionResult> CreateScopeChangeRequest(
        Guid id,
        [FromBody] CreateServiceScopeChangeRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CreateScopeChangeRequestAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/attachments/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadScopeChangeAttachment(
        Guid id,
        Guid scopeChangeRequestId,
        [FromForm] ScopeChangeAttachmentUploadRequest request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Arquivo obrigatorio." });
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedScopeAttachmentExtensions.Contains(extension))
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Tipo de arquivo nao suportado." });
        }

        if (request.File.Length > 25_000_000)
        {
            return BadRequest(new { errorCode = "invalid_attachment_size", message = "Arquivo excede o limite de 25MB." });
        }

        await using var stream = request.File.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "scope-changes");
        var result = await _serviceAppointmentService.AddScopeChangeAttachmentAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId,
            new RegisterServiceScopeChangeAttachmentDto(
                relativeUrl,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length));

        if (result.Success && result.Attachment != null)
        {
            return Ok(result.Attachment);
        }

        _fileStorageService.DeleteFile(relativeUrl);
        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/approve")]
    public async Task<IActionResult> ApproveScopeChange(Guid id, Guid scopeChangeRequestId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ApproveScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/reject")]
    public async Task<IActionResult> RejectScopeChange(
        Guid id,
        Guid scopeChangeRequestId,
        [FromBody] RejectServiceScopeChangeRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RejectScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId,
            request);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/pin/generate")]
    public async Task<IActionResult> GenerateCompletionPin(
        Guid id,
        [FromBody] GenerateServiceCompletionPinRequestDto? request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var payload = request ?? new GenerateServiceCompletionPinRequestDto();
        var result = await _serviceAppointmentService.GenerateCompletionPinAsync(actorUserId, actorRole, id, payload);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term,
                oneTimePin = result.OneTimePin
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/pin/validate")]
    public async Task<IActionResult> ValidateCompletionPin(
        Guid id,
        [FromBody] ValidateServiceCompletionPinRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ValidateCompletionPinAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("{id:guid}/completion")]
    public async Task<IActionResult> GetCompletionTerm(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetCompletionTermAsync(actorUserId, actorRole, id);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/confirm")]
    public async Task<IActionResult> ConfirmCompletion(
        Guid id,
        [FromBody] ConfirmServiceCompletionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ConfirmCompletionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/contest")]
    public async Task<IActionResult> ContestCompletion(
        Guid id,
        [FromBody] ContestServiceCompletionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ContestCompletionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(actorUserId, actorRole, fromUtc, toUtc);
        return Ok(appointments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetByIdAsync(actorUserId, actorRole, id);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("{id:guid}/checklist")]
    public async Task<IActionResult> GetChecklist(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentChecklistService.GetChecklistAsync(actorUserId, actorRole, id);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/checklist/items/{itemId:guid}")]
    public async Task<IActionResult> UpsertChecklistItem(
        Guid id,
        Guid itemId,
        [FromBody] UpsertServiceChecklistItemResponseRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var payload = new UpsertServiceChecklistItemResponseRequestDto(
            itemId,
            request.IsChecked,
            request.Note,
            request.EvidenceUrl,
            request.EvidenceFileName,
            request.EvidenceContentType,
            request.EvidenceSizeBytes,
            request.ClearEvidence);

        var result = await _serviceAppointmentChecklistService.UpsertItemResponseAsync(actorUserId, actorRole, id, payload);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }

    private IActionResult MapFailure(string? errorCode, string? message)
    {
        return errorCode switch
        {
            "forbidden" => Forbid(),
            "provider_not_found" => NotFound(new { errorCode, message }),
            "request_not_found" => NotFound(new { errorCode, message }),
            "appointment_not_found" => NotFound(new { errorCode, message }),
            "rule_not_found" => NotFound(new { errorCode, message }),
            "block_not_found" => NotFound(new { errorCode, message }),
            "appointment_already_exists" => Conflict(new { errorCode, message }),
            "request_window_conflict" => Conflict(new { errorCode, message }),
            "slot_unavailable" => Conflict(new { errorCode, message }),
            "invalid_state" => Conflict(new { errorCode, message }),
            "policy_violation" => Conflict(new { errorCode, message }),
            "duplicate_checkin" => Conflict(new { errorCode, message }),
            "duplicate_start" => Conflict(new { errorCode, message }),
            "invalid_operational_transition" => Conflict(new { errorCode, message }),
            "required_checklist_pending" => Conflict(new { errorCode, message }),
            "checklist_not_configured" => Conflict(new { errorCode, message }),
            "evidence_required" => Conflict(new { errorCode, message }),
            "rule_overlap" => Conflict(new { errorCode, message }),
            "block_overlap" => Conflict(new { errorCode, message }),
            "block_conflict_appointment" => Conflict(new { errorCode, message }),
            "scope_change_pending" => Conflict(new { errorCode, message }),
            "scope_change_expired" => Conflict(new { errorCode, message }),
            "scope_change_not_found" => NotFound(new { errorCode, message }),
            "attachment_limit_exceeded" => Conflict(new { errorCode, message }),
            "invalid_pin" => Conflict(new { errorCode, message }),
            "pin_expired" => Conflict(new { errorCode, message }),
            "pin_locked" => Conflict(new { errorCode, message }),
            "invalid_pin_format" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_reason" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_description" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_value" => BadRequest(new { errorCode, message }),
            "invalid_scope_change" => BadRequest(new { errorCode, message }),
            "invalid_attachment" => BadRequest(new { errorCode, message }),
            "invalid_attachment_size" => BadRequest(new { errorCode, message }),
            "invalid_acceptance_method" => BadRequest(new { errorCode, message }),
            "signature_required" => BadRequest(new { errorCode, message }),
            "contest_reason_required" => BadRequest(new { errorCode, message }),
            "item_not_found" => NotFound(new { errorCode, message }),
            "completion_term_not_found" => NotFound(new { errorCode, message }),
            _ => BadRequest(new { errorCode, message })
        };
    }

    public class ScopeChangeAttachmentUploadRequest
    {
        public IFormFile? File { get; set; }
    }
}
