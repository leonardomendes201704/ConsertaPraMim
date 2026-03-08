using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/landing")]
public sealed class InternalLandingNotificationsController : ControllerBase
{
    private const string HeaderTokenName = "X-Deploy-Token";

    private readonly IConfiguration _configuration;
    private readonly ILandingAdminNotificationService _landingAdminNotificationService;
    private readonly ILogger<InternalLandingNotificationsController> _logger;

    public InternalLandingNotificationsController(
        IConfiguration configuration,
        ILandingAdminNotificationService landingAdminNotificationService,
        ILogger<InternalLandingNotificationsController> logger)
    {
        _configuration = configuration;
        _landingAdminNotificationService = landingAdminNotificationService;
        _logger = logger;
    }

    [HttpPost("access")]
    public async Task<IActionResult> NotifyAccess(
        [FromBody] NotifyLandingAccessRequestDto request,
        CancellationToken cancellationToken)
    {
        var configuredToken = ResolveWebhookToken();
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogWarning("DeployNotifications:WebhookToken nao configurado. Evento de acesso da landing ignorado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errorCode = "landing_access_not_configured",
                message = "DeployNotifications:WebhookToken nao configurado."
            });
        }

        var tokenFromHeader = Request.Headers[HeaderTokenName].FirstOrDefault();
        if (!SecureEquals(configuredToken, tokenFromHeader))
        {
            return Unauthorized(new
            {
                errorCode = "landing_access_invalid_token",
                message = "Token de webhook invalido."
            });
        }

        await _landingAdminNotificationService.NotifyLandingAccessAsync(request, cancellationToken);

        return Ok(new
        {
            processed = true
        });
    }

    private string? ResolveWebhookToken()
    {
        var configured = _configuration["DeployNotifications:WebhookToken"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        configured = Environment.GetEnvironmentVariable("CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN");
        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private static bool SecureEquals(string expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided.Trim());
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
