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
            "appointment_already_exists" => Conflict(new { errorCode, message }),
            "slot_unavailable" => Conflict(new { errorCode, message }),
            _ => BadRequest(new { errorCode, message })
        };
    }
}
