using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public record NotificationRequest(string Recipient, string Subject, string Message, string? ActionUrl = null);

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] NotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient)) return BadRequest("Recipient is required.");
        if (string.IsNullOrWhiteSpace(request.Subject)) return BadRequest("Subject is required.");
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Message is required.");

        await _notificationService.SendNotificationAsync(request.Recipient, request.Subject, request.Message, request.ActionUrl);
        return Ok();
    }
}
