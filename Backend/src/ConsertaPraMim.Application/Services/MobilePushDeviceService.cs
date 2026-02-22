using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class MobilePushDeviceService : IMobilePushDeviceService
{
    private static readonly HashSet<string> AllowedAppKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "client",
        "provider",
        "admin"
    };

    private static readonly HashSet<string> AllowedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android",
        "ios",
        "web"
    };

    private readonly IMobilePushDeviceRepository _mobilePushDeviceRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<MobilePushDeviceService> _logger;

    public MobilePushDeviceService(
        IMobilePushDeviceRepository mobilePushDeviceRepository,
        IUserRepository userRepository,
        ILogger<MobilePushDeviceService> logger)
    {
        _mobilePushDeviceRepository = mobilePushDeviceRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<MobilePushDeviceRegistrationResponseDto> RegisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceRegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Usuario invalido para registro de push.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("Usuario autenticado nao encontrado.");
        }

        var normalizedAppKind = NormalizeAppKind(appKind);
        var normalizedToken = NormalizeToken(request.Token);
        var normalizedPlatform = NormalizePlatform(request.Platform);
        var normalizedInstallationId = NormalizeInstallationId(
            request.InstallationId,
            request.DeviceId,
            normalizedToken);
        var normalizedDeviceId = NormalizeOptionalField(request.DeviceId, 200);
        var now = DateTime.UtcNow;

        var existingByToken = await _mobilePushDeviceRepository
            .GetByTokenAndAppKindAsync(normalizedToken, normalizedAppKind, cancellationToken);
        var existingByInstallation = await _mobilePushDeviceRepository
            .GetByInstallationIdAndAppKindAsync(normalizedInstallationId, normalizedAppKind, cancellationToken);

        MobilePushDevice? deviceToPersist;
        if (existingByToken != null && existingByInstallation != null && existingByToken.Id != existingByInstallation.Id)
        {
            existingByInstallation.IsActive = false;
            existingByInstallation.RevokedAtUtc = now;
            existingByInstallation.LastFailureAtUtc = now;
            existingByInstallation.LastFailureReason = "installation_conflict_merged";
            existingByInstallation.UpdatedAt = now;
            await _mobilePushDeviceRepository.UpdateAsync(existingByInstallation, cancellationToken);

            deviceToPersist = existingByToken;
        }
        else
        {
            deviceToPersist = existingByInstallation ?? existingByToken;
        }

        if (deviceToPersist == null)
        {
            deviceToPersist = new MobilePushDevice
            {
                UserId = userId,
                Token = normalizedToken,
                Platform = normalizedPlatform,
                AppKind = normalizedAppKind,
                InstallationId = normalizedInstallationId,
                DeviceId = normalizedDeviceId,
                DeviceModel = NormalizeOptionalField(request.DeviceModel, 200),
                OsVersion = NormalizeOptionalField(request.OsVersion, 100),
                AppVersion = NormalizeOptionalField(request.AppVersion, 64),
                TimeZone = NormalizeOptionalField(request.TimeZone, 128),
                IsActive = true,
                LastSeenAtUtc = now,
                LastRegisteredAtUtc = now,
                RevokedAtUtc = null,
                LastFailureAtUtc = null,
                LastFailureReason = null,
                UpdatedAt = now
            };

            await _mobilePushDeviceRepository.AddAsync(deviceToPersist, cancellationToken);
            _logger.LogInformation(
                "Mobile push device created. UserId={UserId} AppKind={AppKind} InstallationId={InstallationId} Platform={Platform}",
                userId,
                normalizedAppKind,
                normalizedInstallationId,
                normalizedPlatform);
        }
        else
        {
            var oldUserId = deviceToPersist.UserId;
            var oldToken = deviceToPersist.Token;
            var oldInstallationId = deviceToPersist.InstallationId;

            deviceToPersist.UserId = userId;
            deviceToPersist.Token = normalizedToken;
            deviceToPersist.Platform = normalizedPlatform;
            deviceToPersist.AppKind = normalizedAppKind;
            deviceToPersist.InstallationId = normalizedInstallationId;
            deviceToPersist.DeviceId = normalizedDeviceId;
            deviceToPersist.DeviceModel = NormalizeOptionalField(request.DeviceModel, 200);
            deviceToPersist.OsVersion = NormalizeOptionalField(request.OsVersion, 100);
            deviceToPersist.AppVersion = NormalizeOptionalField(request.AppVersion, 64);
            deviceToPersist.TimeZone = NormalizeOptionalField(request.TimeZone, 128);
            deviceToPersist.IsActive = true;
            deviceToPersist.LastSeenAtUtc = now;
            deviceToPersist.LastRegisteredAtUtc = now;
            deviceToPersist.RevokedAtUtc = null;
            deviceToPersist.LastFailureAtUtc = null;
            deviceToPersist.LastFailureReason = null;
            deviceToPersist.UpdatedAt = now;

            await _mobilePushDeviceRepository.UpdateAsync(deviceToPersist, cancellationToken);

            if (oldUserId != userId)
            {
                _logger.LogInformation(
                    "Mobile push token reassigned to another user. PreviousUserId={OldUserId} NewUserId={NewUserId} AppKind={AppKind} InstallationId={InstallationId}",
                    oldUserId,
                    userId,
                    normalizedAppKind,
                    normalizedInstallationId);
            }

            if (!string.Equals(oldToken, normalizedToken, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Mobile push token rotated. UserId={UserId} AppKind={AppKind} InstallationId={InstallationId}",
                    userId,
                    normalizedAppKind,
                    normalizedInstallationId);
            }

            if (!string.Equals(oldInstallationId, normalizedInstallationId, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Mobile push installation relinked. UserId={UserId} AppKind={AppKind} OldInstallationId={OldInstallationId} NewInstallationId={NewInstallationId}",
                    userId,
                    normalizedAppKind,
                    oldInstallationId,
                    normalizedInstallationId);
            }
        }

        await _mobilePushDeviceRepository.DeactivateByUserAndInstallationIdExceptIdAsync(
            userId,
            normalizedAppKind,
            normalizedInstallationId,
            deviceToPersist.Id,
            "installation_replaced",
            cancellationToken);

        return new MobilePushDeviceRegistrationResponseDto(
            deviceToPersist.Id,
            deviceToPersist.AppKind,
            deviceToPersist.Platform,
            deviceToPersist.IsActive,
            deviceToPersist.LastRegisteredAtUtc);
    }

    public async Task<MobilePushDeviceUnregisterResponseDto> UnregisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceUnregisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Usuario invalido para desregistro de push.");
        }

        var normalizedAppKind = NormalizeAppKind(appKind);
        var normalizedToken = NormalizeOptionalField(request.Token, 4096);
        var normalizedInstallationId = NormalizeOptionalField(request.InstallationId, 200);
        var normalizedDeviceId = NormalizeOptionalField(request.DeviceId, 200);

        if (string.IsNullOrWhiteSpace(normalizedToken) &&
            string.IsNullOrWhiteSpace(normalizedInstallationId) &&
            string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            throw new InvalidOperationException("Informe token, installationId ou deviceId para desregistrar notificacao push.");
        }

        var totalDeactivated = 0;
        if (!string.IsNullOrWhiteSpace(normalizedToken))
        {
            totalDeactivated += await _mobilePushDeviceRepository.DeactivateByUserAndTokenAsync(
                userId,
                normalizedAppKind,
                normalizedToken,
                "user_logout",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(normalizedInstallationId))
        {
            totalDeactivated += await _mobilePushDeviceRepository.DeactivateByUserAndInstallationIdAsync(
                userId,
                normalizedAppKind,
                normalizedInstallationId,
                "user_logout",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            totalDeactivated += await _mobilePushDeviceRepository.DeactivateByUserAndDeviceIdAsync(
                userId,
                normalizedAppKind,
                normalizedDeviceId,
                "user_logout",
                cancellationToken);
        }

        _logger.LogInformation(
            "Mobile push unregister executed. UserId={UserId} AppKind={AppKind} TotalDeactivated={TotalDeactivated}",
            userId,
            normalizedAppKind,
            totalDeactivated);

        return new MobilePushDeviceUnregisterResponseDto(
            totalDeactivated,
            normalizedAppKind,
            DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<MobilePushDeviceDiagnosticDto>> GetDiagnosticsAsync(
        string? appKind,
        bool onlyActive,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var normalizedAppKind = string.IsNullOrWhiteSpace(appKind)
            ? null
            : NormalizeAppKind(appKind);

        var devices = await _mobilePushDeviceRepository.GetLatestAsync(
            normalizedAppKind,
            onlyActive,
            safeLimit,
            cancellationToken);

        return devices
            .Select(device => new MobilePushDeviceDiagnosticDto(
                device.Id,
                device.UserId,
                device.AppKind,
                device.Platform,
                device.InstallationId,
                device.DeviceId,
                device.DeviceModel,
                device.OsVersion,
                device.AppVersion,
                device.TimeZone,
                device.IsActive,
                device.LastSeenAtUtc,
                device.LastRegisteredAtUtc,
                device.LastDeliveredAtUtc,
                device.LastFailureAtUtc,
                device.LastFailureReason,
                device.RevokedAtUtc,
                MaskToken(device.Token)))
            .ToList();
    }

    private static string NormalizeAppKind(string appKind)
    {
        var normalized = (appKind ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedAppKinds.Contains(normalized))
        {
            throw new InvalidOperationException("Canal mobile invalido para push.");
        }

        return normalized;
    }

    private static string NormalizePlatform(string platform)
    {
        var normalized = string.IsNullOrWhiteSpace(platform)
            ? "android"
            : platform.Trim().ToLowerInvariant();

        if (!AllowedPlatforms.Contains(normalized))
        {
            throw new InvalidOperationException("Plataforma de push invalida. Use android, ios ou web.");
        }

        return normalized;
    }

    private static string NormalizeToken(string token)
    {
        var normalized = NormalizeOptionalField(token, 4096);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Token de notificacao push e obrigatorio.");
        }

        return normalized;
    }

    private static string NormalizeInstallationId(string? installationId, string? deviceId, string token)
    {
        var normalized = NormalizeOptionalField(installationId, 200);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        var normalizedDeviceId = NormalizeOptionalField(deviceId, 200);
        if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
        {
            return $"legacy-device-{normalizedDeviceId}";
        }

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"legacy-token-{Convert.ToHexString(tokenHash)[..24]}";
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "n/a";
        }

        var normalized = token.Trim();
        if (normalized.Length <= 10)
        {
            return $"***{normalized[^3..]}";
        }

        return $"{normalized[..4]}***{normalized[^6..]}";
    }

    private static string? NormalizeOptionalField(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return trimmed[..maxLength];
        }

        return trimmed;
    }
}
