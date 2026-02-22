using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly object ApkPublicationStoreSync = new();
    private static readonly JsonSerializerOptions ApkPublicationJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly IConfiguration _configuration;
    private readonly IMobilePushNotificationService _mobilePushNotificationService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<InternalDeployNotificationsController> _logger;

    public InternalDeployNotificationsController(
        IConfiguration configuration,
        IMobilePushNotificationService mobilePushNotificationService,
        IWebHostEnvironment webHostEnvironment,
        ILogger<InternalDeployNotificationsController> logger)
    {
        _configuration = configuration;
        _mobilePushNotificationService = mobilePushNotificationService;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    [HttpGet("apk-publication")]
    public IActionResult GetApkPublicationMetadata(CancellationToken cancellationToken)
    {
        var storePath = ResolveApkPublicationStorePath();
        lock (ApkPublicationStoreSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var store = ReadApkPublicationStore(storePath);
            return Ok(store);
        }
    }

    [HttpPost("apk-publication")]
    public IActionResult UpsertApkPublicationMetadata(
        [FromBody] ApkPublicationMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var configuredToken = ResolveDeployWebhookToken();
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogWarning("DeployNotifications:WebhookToken nao configurado. Metadado de APK ignorado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errorCode = "apk_publication_not_configured",
                message = "DeployNotifications:WebhookToken nao configurado."
            });
        }

        var tokenFromHeader = Request.Headers[HeaderTokenName].FirstOrDefault();
        if (!SecureEquals(configuredToken, tokenFromHeader))
        {
            return Unauthorized(new
            {
                errorCode = "apk_publication_invalid_token",
                message = "Token de webhook invalido."
            });
        }

        var appKind = NormalizePublicationAppKind(request.AppKind);
        if (string.IsNullOrWhiteSpace(appKind))
        {
            return BadRequest(new
            {
                errorCode = "apk_publication_invalid_app_kind",
                message = "appKind deve ser client, provider ou admin."
            });
        }

        var fileName = (request.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new
            {
                errorCode = "apk_publication_invalid_file_name",
                message = "fileName e obrigatorio."
            });
        }

        var publishedAtUtc = request.PublishedAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var storePath = ResolveApkPublicationStorePath();

        ApkPublicationMetadataStore store;
        lock (ApkPublicationStoreSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            store = ReadApkPublicationStore(storePath);
            var existing = store.Items.FirstOrDefault(item =>
                item.AppKind.Equals(appKind, StringComparison.OrdinalIgnoreCase) &&
                item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new ApkPublicationMetadataItem
                {
                    AppKind = appKind,
                    FileName = fileName
                };
                store.Items.Add(existing);
            }

            existing.PublishedAtUtc = publishedAtUtc;
            existing.ReleaseVersion = string.IsNullOrWhiteSpace(request.ReleaseVersion) ? null : request.ReleaseVersion.Trim();
            existing.RunId = string.IsNullOrWhiteSpace(request.RunId) ? null : request.RunId.Trim();
            store.UpdatedAtUtc = DateTimeOffset.UtcNow;

            WriteApkPublicationStore(storePath, store);
        }

        return Ok(new
        {
            appKind,
            fileName,
            publishedAtUtc
        });
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

    private static string? NormalizePublicationAppKind(string? appKind)
    {
        if (string.IsNullOrWhiteSpace(appKind))
        {
            return null;
        }

        var normalized = appKind.Trim().ToLowerInvariant();
        return normalized is "provider" or "client" or "admin"
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

    private string ResolveApkPublicationStorePath()
    {
        var configured = _configuration["DeployNotifications:ApkPublicationStorePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "uploads", "apk-publications.json");
    }

    private static ApkPublicationMetadataStore ReadApkPublicationStore(string storePath)
    {
        if (!System.IO.File.Exists(storePath))
        {
            return new ApkPublicationMetadataStore();
        }

        try
        {
            var raw = System.IO.File.ReadAllText(storePath);
            var store = JsonSerializer.Deserialize<ApkPublicationMetadataStore>(raw, ApkPublicationJsonOptions);
            return store ?? new ApkPublicationMetadataStore();
        }
        catch
        {
            return new ApkPublicationMetadataStore();
        }
    }

    private static void WriteApkPublicationStore(string storePath, ApkPublicationMetadataStore store)
    {
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = JsonSerializer.Serialize(store, ApkPublicationJsonOptions);
        System.IO.File.WriteAllText(storePath, payload);
    }

    public sealed class ApkReleasePushRequest
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? FileserverUrl { get; set; }
        public string? TargetAppKind { get; set; } = "provider";
        public string? ReleaseVersion { get; set; }
    }

    public sealed class ApkPublicationMetadataRequest
    {
        public string? AppKind { get; set; }
        public string? FileName { get; set; }
        public DateTimeOffset? PublishedAtUtc { get; set; }
        public string? ReleaseVersion { get; set; }
        public string? RunId { get; set; }
    }

    public sealed class ApkPublicationMetadataStore
    {
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<ApkPublicationMetadataItem> Items { get; set; } = [];
    }

    public sealed class ApkPublicationMetadataItem
    {
        public string AppKind { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public string? ReleaseVersion { get; set; }
        public string? RunId { get; set; }
    }
}
