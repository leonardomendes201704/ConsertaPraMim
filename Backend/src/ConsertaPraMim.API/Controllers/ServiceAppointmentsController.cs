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
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public ServiceAppointmentsController(IServiceAppointmentService serviceAppointmentService)
    {
        _serviceAppointmentService = serviceAppointmentService;
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
            "rule_overlap" => Conflict(new { errorCode, message }),
            "block_overlap" => Conflict(new { errorCode, message }),
            "block_conflict_appointment" => Conflict(new { errorCode, message }),
            _ => BadRequest(new { errorCode, message })
        };
    }
}
