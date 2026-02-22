using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/deploy")]
public class InternalDeployNotificationsController : ControllerBase
{
    private const string HeaderTokenName = "X-Deploy-Token";
    private readonly IConfiguration _configuration;
    private readonly IMobilePushNotificationService _mobilePushNotificationService;
    private readonly ILogger<InternalDeployNotificationsController> _logger;

    public InternalDeployNotificationsController(
        IConfiguration configuration,
        IMobilePushNotificationService mobilePushNotificationService,
        ILogger<InternalDeployNotificationsController> logger)
    {
        _configuration = configuration;
        _mobilePushNotificationService = mobilePushNotificationService;
        _logger = logger;
    }

    [HttpPost("apk-release")]
    public async Task<IActionResult> NotifyApkRelease(
        [FromBody] ApkReleasePushRequest request,
        CancellationToken cancellationToken)
    {
        var configuredToken = ResolveDeployWebhookToken();
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogWarning("DeployNotifications:WebhookToken nao configurado. Push de release ignorado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errorCode = "apk_release_push_not_configured",
                message = "DeployNotifications:WebhookToken nao configurado."
            });
        }

        var tokenFromHeader = Request.Headers[HeaderTokenName].FirstOrDefault();
        if (!SecureEquals(configuredToken, tokenFromHeader))
        {
            return Unauthorized(new
            {
                errorCode = "apk_release_push_invalid_token",
                message = "Token de webhook invalido."
            });
        }

        var targetAppKind = NormalizeTargetAppKind(request.TargetAppKind);
        if (targetAppKind is null)
        {
            return BadRequest(new
            {
                errorCode = "apk_release_push_invalid_target",
                message = "targetAppKind deve ser provider ou client."
            });
        }

        var fileserverUrl = ResolveFileserverUrl(request.FileserverUrl);
        if (string.IsNullOrWhiteSpace(fileserverUrl))
        {
            return BadRequest(new
            {
                errorCode = "apk_release_push_invalid_url",
                message = "Informe fileserverUrl valido."
            });
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? "Novo APK disponivel"
            : request.Title.Trim();
        var body = string.IsNullOrWhiteSpace(request.Body)
            ? "Toque para abrir a central de downloads."
            : request.Body.Trim();

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "apk_release",
            ["actionUrl"] = fileserverUrl,
            ["url"] = fileserverUrl,
            ["targetAppKind"] = targetAppKind
        };

        if (!string.IsNullOrWhiteSpace(request.ReleaseVersion))
        {
            data["releaseVersion"] = request.ReleaseVersion.Trim();
        }

        var attemptedDevices = await _mobilePushNotificationService.SendToAppKindAsync(
            targetAppKind,
            title,
            body,
            actionUrl: fileserverUrl,
            data: data,
            cancellationToken);

        return Ok(new
        {
            targetAppKind,
            attemptedDevices,
            fileserverUrl
        });
    }

    private string? ResolveDeployWebhookToken()
    {
        var configured = _configuration["DeployNotifications:WebhookToken"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        configured = Environment.GetEnvironmentVariable("CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN");
        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private string? ResolveFileserverUrl(string? incomingUrl)
    {
        if (!string.IsNullOrWhiteSpace(incomingUrl))
        {
            if (TryNormalizeHttpUrl(incomingUrl, out var normalizedIncoming))
            {
                return normalizedIncoming;
            }

            return null;
        }

        var host = _configuration["VPS_PUBLIC_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var normalizedHost = host.Trim();
        if (!normalizedHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalizedHost = $"http://{normalizedHost}";
        }

        if (!Uri.TryCreate(normalizedHost, UriKind.Absolute, out var hostUri))
        {
            return null;
        }

        var builder = new UriBuilder(hostUri)
        {
            Scheme = Uri.UriSchemeHttp,
            Port = 8080,
            Path = "/files/apks/"
        };

        return builder.Uri.ToString();
    }

    private static string? NormalizeTargetAppKind(string? appKind)
    {
        var normalized = string.IsNullOrWhiteSpace(appKind)
            ? "provider"
            : appKind.Trim().ToLowerInvariant();

        return normalized is "provider" or "client"
            ? normalized
            : null;
    }

    private static bool TryNormalizeHttpUrl(string rawValue, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.ToString();
        return true;
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

    public sealed class ApkReleasePushRequest
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? FileserverUrl { get; set; }
        public string? TargetAppKind { get; set; } = "provider";
        public string? ReleaseVersion { get; set; }
    }
}
