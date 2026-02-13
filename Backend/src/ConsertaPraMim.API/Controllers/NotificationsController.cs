using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public NotificationsController(INotificationService notificationService, IConfiguration configuration)
    {
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public record NotificationRequest(string Recipient, string Subject, string Message, string? ActionUrl = null);

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] NotificationRequest request)
    {
        if (!IsInternalRequestAuthorized())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Recipient)) return BadRequest("Recipient is required.");
        if (string.IsNullOrWhiteSpace(request.Subject)) return BadRequest("Subject is required.");
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest("Message is required.");

        await _notificationService.SendNotificationAsync(request.Recipient, request.Subject, request.Message, request.ActionUrl);
        return Ok();
    }

    private bool IsInternalRequestAuthorized()
    {
        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var providedValues))
        {
            return false;
        }

        var providedKey = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        var expectedKey = _configuration["InternalNotifications:ApiKey"]
            ?? _configuration["JwtSettings:SecretKey"];

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
