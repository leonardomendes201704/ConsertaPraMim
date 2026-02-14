using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/appointment-reminders")]
public class AdminAppointmentRemindersController : ControllerBase
{
    private readonly IAppointmentReminderService _appointmentReminderService;

    public AdminAppointmentRemindersController(IAppointmentReminderService appointmentReminderService)
    {
        _appointmentReminderService = appointmentReminderService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AppointmentReminderDispatchQueryDto query)
    {
        var result = await _appointmentReminderService.GetDispatchesAsync(query);
        return Ok(result);
    }
}
