using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MobilePushNotificationService : IMobilePushNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFirebasePushSender _firebasePushSender;
    private readonly ILogger<MobilePushNotificationService> _logger;
    private readonly int _activeWindowDays;

    public MobilePushNotificationService(
        IServiceScopeFactory scopeFactory,
        IFirebasePushSender firebasePushSender,
        IConfiguration configuration,
        ILogger<MobilePushNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _firebasePushSender = firebasePushSender;
        _logger = logger;
        _activeWindowDays = ParseInt(configuration["PushNotifications:Devices:ActiveWindowDays"], 90, 1, 3650);
    }

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var deviceRepository = scope.ServiceProvider.GetRequiredService<IMobilePushDeviceRepository>();

        var minLastSeenAtUtc = DateTime.UtcNow.AddDays(-_activeWindowDays);
        var devices = await deviceRepository.GetActiveByUserIdAsync(userId, minLastSeenAtUtc, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug("Nenhum device push ativo para o usuario {UserId}.", userId);
            return;
        }

        _logger.LogInformation(
            "Enviando push para usuario {UserId}. ActiveDevices={DeviceCount} ActiveWindowDays={ActiveWindowDays}.",
            userId,
            devices.Count,
            _activeWindowDays);

        var normalizedData = BuildData(userId, actionUrl, data);
        await SendToDevicesAsync(deviceRepository, devices, title, message, normalizedData, cancellationToken);
    }

    public async Task<int> SendToAppKindAsync(
        string appKind,
        string title,
        string message,
        string? actionUrl = null,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appKind))
        {
            return 0;
        }

        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        using var scope = _scopeFactory.CreateScope();
        var deviceRepository = scope.ServiceProvider.GetRequiredService<IMobilePushDeviceRepository>();
        var minLastSeenAtUtc = DateTime.UtcNow.AddDays(-_activeWindowDays);
        var devices = await deviceRepository.GetActiveByAppKindAsync(normalizedAppKind, minLastSeenAtUtc, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug(
                "Nenhum device push ativo para appKind {AppKind}.",
                normalizedAppKind);
            return 0;
        }

        _logger.LogInformation(
            "Enviando push por appKind {AppKind}. ActiveDevices={DeviceCount} ActiveWindowDays={ActiveWindowDays}.",
            normalizedAppKind,
            devices.Count,
            _activeWindowDays);

        var normalizedData = BuildData(userId: null, actionUrl, data);
        await SendToDevicesAsync(deviceRepository, devices, title, message, normalizedData, cancellationToken);
        return devices.Count;
    }

    public async Task SendToTokensAsync(
        IReadOnlyCollection<string> tokens,
        string title,
        string message,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var normalizedData = BuildData(userId: null, actionUrl: null, additionalData: data);
        var uniqueTokens = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var token in uniqueTokens)
        {
            var result = await _firebasePushSender.SendAsync(
                token,
                title,
                message,
                normalizedData,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Falha ao enviar push direto por token. TokenPrefix={TokenPrefix} Reason={Reason}",
                    token.Length >= 12 ? token[..12] : token,
                    result.FailureReason ?? "unknown");
            }
        }
    }

    private IReadOnlyDictionary<string, string> BuildData(
        Guid? userId,
        string? actionUrl,
        IReadOnlyDictionary<string, string>? additionalData)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["source"] = "consertapramim_api"
        };

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            data["recipientUserId"] = userId.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            data["actionUrl"] = actionUrl.Trim();
        }

        if (additionalData != null)
        {
            foreach (var pair in additionalData)
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

                data[key] = value;
            }
        }

        return data;
    }

    private async Task SendToDevicesAsync(
        IMobilePushDeviceRepository deviceRepository,
        IReadOnlyList<MobilePushDevice> devices,
        string title,
        string message,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var hasChanges = false;
        var successCount = 0;
        var failureCount = 0;
        var deactivatedCount = 0;
        foreach (var device in devices)
        {
            var result = await _firebasePushSender.SendAsync(
                device.Token,
                title,
                message,
                data,
                cancellationToken);

            var now = DateTime.UtcNow;
            device.UpdatedAt = now;
            if (result.IsSuccess)
            {
                successCount++;
                device.LastDeliveredAtUtc = now;
                device.LastFailureAtUtc = null;
                device.LastFailureReason = null;
            }
            else
            {
                failureCount++;
                device.LastFailureAtUtc = now;
                device.LastFailureReason = NormalizeReason(result.FailureReason);
                if (result.ShouldDeactivateToken)
                {
                    deactivatedCount++;
                    device.IsActive = false;
                    device.RevokedAtUtc = now;
                }
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await deviceRepository.UpdateRangeAsync(devices, cancellationToken);
            _logger.LogInformation(
                "Push dispatch concluido. Total={Total} Success={Success} Failure={Failure} Deactivated={Deactivated}.",
                devices.Count,
                successCount,
                failureCount,
                deactivatedCount);
        }
    }

    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "push_delivery_failed";
        }

        var trimmed = reason.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }
}
