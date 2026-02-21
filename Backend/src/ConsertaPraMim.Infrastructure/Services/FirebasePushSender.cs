using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public interface IFirebasePushSender
{
    Task<FirebasePushSendResult> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken = default);
}

public sealed record FirebasePushSendResult(
    bool IsSuccess,
    bool ShouldDeactivateToken,
    string? FailureReason = null);

public class FirebasePushSender : IFirebasePushSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string FirebaseScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string LegacyEndpoint = "https://fcm.googleapis.com/fcm/send";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FirebasePushSender> _logger;
    private readonly SemaphoreSlim _oauthLock = new(1, 1);

    private GoogleCredential? _googleCredential;
    private string? _cachedServiceAccountSourceKey;
    private string? _cachedProjectId;
    private string? _cachedAccessToken;
    private DateTime _cachedAccessTokenExpiresAtUtc = DateTime.MinValue;

    public FirebasePushSender(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FirebasePushSender> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FirebasePushSendResult> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new FirebasePushSendResult(false, ShouldDeactivateToken: true, "invalid_empty_token");
        }

        var normalizedToken = token.Trim();
        var serviceAccountPath = ResolveServiceAccountPath();
        var serviceAccountJson = ResolveServiceAccountJson();
        var hasServiceAccountPath = !string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath);
        var hasServiceAccountJson = !string.IsNullOrWhiteSpace(serviceAccountJson);
        var configuredProjectId = ResolveProjectId(serviceAccountPath, serviceAccountJson);

        if (!string.IsNullOrWhiteSpace(serviceAccountPath) && !hasServiceAccountPath && !hasServiceAccountJson)
        {
            _logger.LogWarning(
                "PushNotifications:Firebase:ServiceAccountPath aponta para arquivo inexistente e nenhum JSON foi informado: {ServiceAccountPath}",
                serviceAccountPath);
        }

        if ((hasServiceAccountPath || hasServiceAccountJson) &&
            !string.IsNullOrWhiteSpace(configuredProjectId))
        {
            return await SendViaHttpV1Async(
                normalizedToken,
                configuredProjectId,
                serviceAccountPath,
                serviceAccountJson,
                title,
                body,
                data,
                cancellationToken);
        }

        if ((hasServiceAccountPath || hasServiceAccountJson) &&
            string.IsNullOrWhiteSpace(configuredProjectId))
        {
            _logger.LogWarning(
                "Service account Firebase encontrado, mas ProjectId nao foi resolvido. Configure PushNotifications:Firebase:ProjectId.");
        }

        var legacyServerKey = ResolveLegacyServerKey();
        if (!string.IsNullOrWhiteSpace(legacyServerKey))
        {
            return await SendViaLegacyAsync(
                normalizedToken,
                legacyServerKey,
                title,
                body,
                data,
                cancellationToken);
        }

        _logger.LogWarning(
            "Firebase push nao configurado. Defina PushNotifications:Firebase:ServiceAccountPath + ProjectId ou PushNotifications:Firebase:ServerKey.");

        return new FirebasePushSendResult(
            IsSuccess: false,
            ShouldDeactivateToken: false,
            FailureReason: "firebase_push_not_configured");
    }

    private async Task<FirebasePushSendResult> SendViaHttpV1Async(
        string token,
        string projectId,
        string? serviceAccountPath,
        string? serviceAccountJson,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(serviceAccountPath, serviceAccountJson, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new FirebasePushSendResult(
                IsSuccess: false,
                ShouldDeactivateToken: false,
                FailureReason: "firebase_access_token_unavailable");
        }

        var endpoint = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";
        var payload = new
        {
            message = new
            {
                token,
                notification = new
                {
                    title = string.IsNullOrWhiteSpace(title) ? "ConsertaPraMim" : title.Trim(),
                    body = string.IsNullOrWhiteSpace(body) ? "Voce tem uma nova notificacao." : body.Trim()
                },
                data = NormalizeData(data),
                android = new
                {
                    priority = "high",
                    notification = new
                    {
                        channel_id = "default",
                        sound = "default"
                    }
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient(nameof(FirebasePushSender));
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new FirebasePushSendResult(true, ShouldDeactivateToken: false);
            }

            var responseBody = await SafeReadBodyAsync(response, cancellationToken);
            var reason = $"firebase_http_v1_{(int)response.StatusCode}";
            var shouldDeactivate = ShouldDeactivateToken(responseBody);

            _logger.LogWarning(
                "Falha ao enviar push via Firebase HTTP v1. Status={StatusCode}, Body={Body}",
                response.StatusCode,
                responseBody);

            return new FirebasePushSendResult(false, shouldDeactivate, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar push via Firebase HTTP v1.");
            return new FirebasePushSendResult(false, ShouldDeactivateToken: false, "firebase_http_v1_exception");
        }
    }

    private async Task<FirebasePushSendResult> SendViaLegacyAsync(
        string token,
        string serverKey,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            to = token,
            priority = "high",
            notification = new
            {
                title = string.IsNullOrWhiteSpace(title) ? "ConsertaPraMim" : title.Trim(),
                body = string.IsNullOrWhiteSpace(body) ? "Voce tem uma nova notificacao." : body.Trim(),
                sound = "default"
            },
            data = NormalizeData(data)
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, LegacyEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", $"key={serverKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient(nameof(FirebasePushSender));
            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await SafeReadBodyAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Falha ao enviar push via FCM legacy. Status={StatusCode}, Body={Body}",
                    response.StatusCode,
                    responseBody);

                return new FirebasePushSendResult(
                    false,
                    ShouldDeactivateToken: response.StatusCode == System.Net.HttpStatusCode.Unauthorized,
                    FailureReason: $"firebase_legacy_{(int)response.StatusCode}");
            }

            var normalized = responseBody.ToLowerInvariant();
            if (normalized.Contains("\"success\":1", StringComparison.Ordinal) ||
                normalized.Contains("\"success\": 1", StringComparison.Ordinal))
            {
                return new FirebasePushSendResult(true, ShouldDeactivateToken: false);
            }

            var shouldDeactivate = normalized.Contains("notregistered", StringComparison.Ordinal) ||
                                   normalized.Contains("invalidregistration", StringComparison.Ordinal);

            return new FirebasePushSendResult(false, shouldDeactivate, "firebase_legacy_delivery_failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar push via FCM legacy.");
            return new FirebasePushSendResult(false, ShouldDeactivateToken: false, "firebase_legacy_exception");
        }
    }

    private async Task<string?> GetAccessTokenAsync(
        string? serviceAccountPath,
        string? serviceAccountJson,
        CancellationToken cancellationToken)
    {
        var sourceKey = BuildServiceAccountSourceKey(serviceAccountPath, serviceAccountJson);
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && now < _cachedAccessTokenExpiresAtUtc)
        {
            return _cachedAccessToken;
        }

        await _oauthLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && now < _cachedAccessTokenExpiresAtUtc)
            {
                return _cachedAccessToken;
            }

            if (!string.Equals(_cachedServiceAccountSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase) || _googleCredential == null)
            {
                if (!string.IsNullOrWhiteSpace(serviceAccountPath))
                {
                    _googleCredential = GoogleCredential.FromFile(serviceAccountPath).CreateScoped(FirebaseScope);
                }
                else if (!string.IsNullOrWhiteSpace(serviceAccountJson))
                {
                    using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(serviceAccountJson));
                    _googleCredential = GoogleCredential.FromStream(jsonStream).CreateScoped(FirebaseScope);
                }
                else
                {
                    return null;
                }

                _cachedServiceAccountSourceKey = sourceKey;
                _cachedAccessToken = null;
                _cachedAccessTokenExpiresAtUtc = DateTime.MinValue;
            }

            var token = await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync(null, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            _cachedAccessToken = token;
            _cachedAccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(45);
            return _cachedAccessToken;
        }
        finally
        {
            _oauthLock.Release();
        }
    }

    private static IReadOnlyDictionary<string, string> NormalizeData(IReadOnlyDictionary<string, string>? data)
    {
        if (data == null || data.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in data)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (key.Length > 128)
            {
                key = key[..128];
            }

            if (value.Length > 2048)
            {
                value = value[..2048];
            }

            normalized[key] = value;
        }

        return normalized;
    }

    private static bool ShouldDeactivateToken(string? responseBody)
    {
        var normalized = (responseBody ?? string.Empty).ToLowerInvariant();
        return normalized.Contains("unregistered", StringComparison.Ordinal) ||
               normalized.Contains("invalidregistration", StringComparison.Ordinal) ||
               normalized.Contains("registration-token-not-registered", StringComparison.Ordinal) ||
               normalized.Contains("invalid registration token", StringComparison.Ordinal);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string? ResolveServiceAccountPath()
    {
        var configured = _configuration["PushNotifications:Firebase:ServiceAccountPath"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("CPM_FIREBASE_SERVICE_ACCOUNT_PATH");
        }

        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private string? ResolveServiceAccountJson()
    {
        var inlineJson = _configuration["PushNotifications:Firebase:ServiceAccountJson"];
        if (string.IsNullOrWhiteSpace(inlineJson))
        {
            inlineJson = Environment.GetEnvironmentVariable("CPM_FIREBASE_SERVICE_ACCOUNT_JSON");
        }

        if (!string.IsNullOrWhiteSpace(inlineJson))
        {
            return inlineJson.Trim();
        }

        var base64 = _configuration["PushNotifications:Firebase:ServiceAccountJsonBase64"];
        if (string.IsNullOrWhiteSpace(base64))
        {
            base64 = Environment.GetEnvironmentVariable("CPM_FIREBASE_SERVICE_ACCOUNT_JSON_BASE64");
        }

        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var jsonBytes = Convert.FromBase64String(base64.Trim());
            return Encoding.UTF8.GetString(jsonBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao decodificar PushNotifications:Firebase:ServiceAccountJsonBase64.");
            return null;
        }
    }

    private string? ResolveLegacyServerKey()
    {
        var configured = _configuration["PushNotifications:Firebase:ServerKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("CPM_FIREBASE_SERVER_KEY");
        }

        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private string? ResolveProjectId(string? serviceAccountPath, string? serviceAccountJson)
    {
        var configured = _configuration["PushNotifications:Firebase:ProjectId"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("CPM_FIREBASE_PROJECT_ID");
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            _cachedProjectId = configured.Trim();
            return _cachedProjectId;
        }

        if (!string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            try
            {
                using var document = JsonDocument.Parse(serviceAccountJson);
                if (document.RootElement.TryGetProperty("project_id", out var projectIdElement))
                {
                    var projectId = projectIdElement.GetString();
                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        _cachedProjectId = projectId.Trim();
                        return _cachedProjectId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nao foi possivel ler project_id do service account JSON inline do Firebase.");
            }
        }

        if (string.IsNullOrWhiteSpace(serviceAccountPath) || !File.Exists(serviceAccountPath))
        {
            return _cachedProjectId;
        }

        try
        {
            using var stream = File.OpenRead(serviceAccountPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("project_id", out var projectIdElement))
            {
                var projectId = projectIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    _cachedProjectId = projectId.Trim();
                    return _cachedProjectId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel ler project_id do service account do Firebase.");
        }

        return null;
    }

    private static string? BuildServiceAccountSourceKey(string? serviceAccountPath, string? serviceAccountJson)
    {
        if (!string.IsNullOrWhiteSpace(serviceAccountPath))
        {
            return $"path:{Path.GetFullPath(serviceAccountPath.Trim())}";
        }

        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(serviceAccountJson));
        return $"json:{Convert.ToHexString(bytes)}";
    }
}
