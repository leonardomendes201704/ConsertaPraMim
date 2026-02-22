using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MobilePushNotificationService : IMobilePushNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFirebasePushSender _firebasePushSender;
    private readonly ILogger<MobilePushNotificationService> _logger;

    public MobilePushNotificationService(
        IServiceScopeFactory scopeFactory,
        IFirebasePushSender firebasePushSender,
        ILogger<MobilePushNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _firebasePushSender = firebasePushSender;
        _logger = logger;
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

        var devices = await deviceRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug("Nenhum device push ativo para o usuario {UserId}.", userId);
            return;
        }

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
        var devices = await deviceRepository.GetActiveByAppKindAsync(normalizedAppKind, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug(
                "Nenhum device push ativo para appKind {AppKind}.",
                normalizedAppKind);
            return 0;
        }

        var normalizedData = BuildData(userId: null, actionUrl, data);
        await SendToDevicesAsync(deviceRepository, devices, title, message, normalizedData, cancellationToken);
        return devices.Count;
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
                device.LastDeliveredAtUtc = now;
                device.LastFailureAtUtc = null;
                device.LastFailureReason = null;
            }
            else
            {
                device.LastFailureAtUtc = now;
                device.LastFailureReason = NormalizeReason(result.FailureReason);
                if (result.ShouldDeactivateToken)
                {
                    device.IsActive = false;
                }
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await deviceRepository.UpdateRangeAsync(devices, cancellationToken);
        }
    }

    private string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "push_delivery_failed";
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > 500)
        {
            return trimmed[..500];
        }

        return trimmed;
    }
}
